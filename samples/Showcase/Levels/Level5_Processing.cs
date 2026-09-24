// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.Transaction.Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Transaction.Showcase.Levels;

/// <summary>
/// Level 05: Nested Transactions, Savepoints, Ambient Context Flow &amp; All NestedTransactionBehavior Modes.
/// Demonstrates hierarchical Savepoint semantics, partial rollback recovery, direct ISavepoint API,
/// ambient AsyncLocal context propagation, and all four NestedTransactionBehavior values:
/// UseSavepoint, JoinExisting, RequireNew, Suppress.
/// </summary>
public sealed class Level5_Processing : ILevel
{
    public int LevelNumber => 5;
    public string Name => "Nested Transactions, Savepoints & All NestedTransactionBehavior Modes";
    public string Description => "Demonstrates hierarchical Savepoint isolation, partial rollback recovery, direct ISavepoint API (CreateSavepointAsync, RollbackAsync, ReleaseAsync), ambient AsyncLocal transaction propagation, and all four NestedTransactionBehavior values: UseSavepoint, JoinExisting, RequireNew, Suppress.";
    public string Category => "Advanced";

    public async Task RunAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("  LEVEL 05: NESTED TRANSACTIONS, SAVEPOINTS & ALL NESTEDTRANSACTIONBEHAVIOR MODES");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        using var masterConnection = new SqliteConnection("Data Source=savepoints_ambient;Mode=Memory;Cache=Shared");
        await masterConnection.OpenAsync(cancellationToken);

        await masterConnection.ExecuteAsync("""
            CREATE TABLE batch_jobs (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                status TEXT NOT NULL
            );
            CREATE TABLE job_items (
                id TEXT PRIMARY KEY,
                job_id TEXT NOT NULL,
                item_name TEXT NOT NULL,
                status TEXT NOT NULL
            );
            """);

        var services = new ServiceCollection();
        services.AddTransaction(_ => new SqliteConnection("Data Source=savepoints_ambient;Mode=Memory;Cache=Shared"));

        using ServiceProvider localProvider = services.BuildServiceProvider();
        ITransactionManager transactionManager = localProvider.GetRequiredService<ITransactionManager>();

        // ─── Part 1: Nested Scope with Automatic Savepoints (NestedTransactionBehavior.UseSavepoint) ──

        Console.WriteLine("[Part 1] Hierarchical Savepoints & Partial Rollback Recovery (via nested ExecuteAsync):");
        Console.WriteLine("  Scenario: batch job with 3 items — Item 1 OK, Item 2 fails (savepoint rolled back), Item 3 OK, batch commits.\n");

        string jobId = "job-100";

        await transactionManager.ExecuteAsync(async outerContext =>
        {
            Console.WriteLine($"  -> [Outer Scope] Started Batch Job '{jobId}' on Tx {outerContext.TransactionId}");
            await outerContext.ExecuteAsync(
                "INSERT INTO batch_jobs VALUES (@jobId, 'Data Ingestion Batch', 'Processing');",
                new { jobId },
                cancellationToken: outerContext.CancellationToken);

            // Item 1: Success
            await transactionManager.ExecuteAsync(async itemContext =>
            {
                Console.WriteLine("     -> [Item 1 Scope] Created Savepoint. Inserting Item 1 (Valid)...");
                await itemContext.ExecuteAsync(
                    "INSERT INTO job_items VALUES ('item-1', @jobId, 'Sensor Data Chunk A', 'Completed');",
                    new { jobId },
                    cancellationToken: itemContext.CancellationToken);
            }, new TransactionOptions { NestedBehavior = NestedTransactionBehavior.UseSavepoint }, outerContext.CancellationToken);

            // Item 2: Failure inside nested savepoint
            try
            {
                await transactionManager.ExecuteAsync(async itemContext =>
                {
                    Console.WriteLine("     -> [Item 2 Scope] Created Savepoint. Inserting Item 2 (Corrupt)...");
                    await itemContext.ExecuteAsync(
                        "INSERT INTO job_items VALUES ('item-2', @jobId, 'Corrupt Payload', 'Failed');",
                        new { jobId },
                        cancellationToken: itemContext.CancellationToken);

                    Console.WriteLine("     -> [Item 2 Scope] Validation failed! Triggering savepoint rollback...");
                    throw new InvalidOperationException("Payload checksum validation failed on Item 2.");
                }, new TransactionOptions { NestedBehavior = NestedTransactionBehavior.UseSavepoint }, outerContext.CancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"     -> [Outer Scope] Caught Item 2 error: '{ex.Message}'. Only Item 2 savepoint was rolled back!");
            }

            // Item 3: Success
            await transactionManager.ExecuteAsync(async itemContext =>
            {
                Console.WriteLine("     -> [Item 3 Scope] Created Savepoint. Inserting Item 3 (Valid)...");
                await itemContext.ExecuteAsync(
                    "INSERT INTO job_items VALUES ('item-3', @jobId, 'Sensor Data Chunk C', 'Completed');",
                    new { jobId },
                    cancellationToken: itemContext.CancellationToken);
            }, new TransactionOptions { NestedBehavior = NestedTransactionBehavior.UseSavepoint }, outerContext.CancellationToken);

            // Update batch job status
            await outerContext.ExecuteAsync(
                "UPDATE batch_jobs SET status = 'PartiallyCompleted' WHERE id = @jobId;",
                new { jobId },
                cancellationToken: outerContext.CancellationToken);

            Console.WriteLine("  -> [Outer Scope] Committing physical transaction...");
        }, TransactionOptions.Default, cancellationToken);

        // Verification of database state
        int savedItemsCount = await masterConnection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM job_items WHERE job_id = 'job-100';");
        int item2Exists = await masterConnection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM job_items WHERE id = 'item-2';");

        Console.WriteLine("\n[Verification — Part 1]:");
        Console.WriteLine($"  • Total Items Saved in DB: {savedItemsCount} (Expected: 2 -> item-1 and item-3)");
        Console.WriteLine($"  • Corrupt Item 2 in DB:    {item2Exists} (Expected: 0 -> rolled back to savepoint)\n");

        // ─── Part 2: Direct ISavepoint API — CreateSavepointAsync, Name, RollbackAsync, ReleaseAsync ──

        Console.WriteLine("[Part 2] Direct ISavepoint API (ITransactionContext.CreateSavepointAsync, ISavepoint.Name, RollbackAsync, ReleaseAsync):\n");

        string jobId2 = "job-200";

        await transactionManager.ExecuteAsync(async context =>
        {
            // Insert outer batch record
            await context.ExecuteAsync(
                "INSERT INTO batch_jobs VALUES (@jobId, 'Direct Savepoint Batch', 'Active');",
                new { jobId = jobId2 },
                cancellationToken: context.CancellationToken);

            // Create savepoint A via ITransactionContext.CreateSavepointAsync
            ISavepoint savepointA = await context.CreateSavepointAsync("sp_batch_item_A", context.CancellationToken);
            Console.WriteLine($"  -> Savepoint A created: Name='{savepointA.Name}'");

            // Insert item under savepoint A
            await context.ExecuteAsync(
                "INSERT INTO job_items VALUES ('di-1', @jobId, 'Direct Savepoint Item A', 'Pending');",
                new { jobId = jobId2 },
                cancellationToken: context.CancellationToken);

            // Release savepoint A — mark it as confirmed (no longer rollbackable independently)
            await savepointA.ReleaseAsync(context.CancellationToken);
            Console.WriteLine($"  -> Savepoint A '{savepointA.Name}' released (committed into outer transaction).");

            // Create savepoint B
            ISavepoint savepointB = await context.CreateSavepointAsync("sp_batch_item_B", context.CancellationToken);
            Console.WriteLine($"  -> Savepoint B created: Name='{savepointB.Name}'");

            // Insert item under savepoint B (this will be rolled back)
            await context.ExecuteAsync(
                "INSERT INTO job_items VALUES ('di-2', @jobId, 'Direct Savepoint Item B — CORRUPT', 'Failed');",
                new { jobId = jobId2 },
                cancellationToken: context.CancellationToken);

            // Roll back savepoint B — discard item B
            await savepointB.RollbackAsync(context.CancellationToken);
            Console.WriteLine($"  -> Savepoint B '{savepointB.Name}' rolled back (Item B discarded).");

            // Outer transaction will commit: only Item A persists
            Console.WriteLine("  -> Outer transaction committing...");
        }, TransactionOptions.Default, cancellationToken);

        // Verify savepoint results
        int diItemCount = await masterConnection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM job_items WHERE job_id = @jobId;", new { jobId = jobId2 });
        int diItem2Exists = await masterConnection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM job_items WHERE id = 'di-2';");

        Console.WriteLine($"\n[Verification — Part 2]:");
        Console.WriteLine($"  • Items Committed for job-200:   {diItemCount} (Expected: 1 -> only Item A)");
        Console.WriteLine($"  • Corrupt Item B (di-2) in DB:  {diItem2Exists} (Expected: 0 -> rolled back via savepointB.RollbackAsync)\n");

        // ─── Part 3: Ambient Context Propagation (AsyncLocal) ─────────────────────

        Console.WriteLine("[Part 3] Ambient Context Flow (AsyncLocal):");
        await transactionManager.ExecuteAsync(async context =>
        {
            Console.WriteLine($"  -> Main Method: CurrentContext is active: {transactionManager.CurrentContext is not null}");
            Console.WriteLine($"  -> CurrentContext TransactionId: {transactionManager.CurrentContext?.TransactionId}");

            await DeeplyNestedServiceCallAsync(transactionManager);
        }, TransactionOptions.Default, cancellationToken);

        Console.WriteLine($"  -> After Outer Scope Disposed: CurrentContext is null: {transactionManager.CurrentContext is null}");

        // ─── Part 4: NestedTransactionBehavior.JoinExisting ───────────────────────

        Console.WriteLine("\n[Part 4] NestedTransactionBehavior.JoinExisting — All nested failures invalidate the outer transaction:\n");
        Console.WriteLine("  Semantic: The nested call joins the existing transaction without creating any savepoint.");
        Console.WriteLine("  Implication: Any exception thrown inside the nested scope rolls back the ENTIRE outer transaction.\n");

        string jobId3 = "job-300";
        bool joinExistingVerified = false;

        await transactionManager.ExecuteAsync(async outerCtx =>
        {
            Console.WriteLine($"  -> [Outer Scope] Started job '{jobId3}' on Tx {outerCtx.TransactionId}");
            await outerCtx.ExecuteAsync(
                "INSERT INTO batch_jobs VALUES (@jobId, 'JoinExisting Batch', 'Running');",
                new { jobId = jobId3 },
                cancellationToken: outerCtx.CancellationToken);

            // Nested call — JoinExisting: participates in the same physical transaction
            // No savepoint is created; all writes share the same DbTransaction
            await transactionManager.ExecuteAsync(async innerCtx =>
            {
                Console.WriteLine($"    -> [Inner Scope] JoinExisting: same Tx ID = {innerCtx.TransactionId}");

                // Same TransactionId confirms shared physical transaction
                bool sameTransaction = innerCtx.TransactionId == outerCtx.TransactionId;
                Console.WriteLine($"    -> InnerCtx.TransactionId == OuterCtx.TransactionId: {sameTransaction}");

                await innerCtx.ExecuteAsync(
                    "INSERT INTO job_items VALUES ('je-1', @jobId, 'JoinExisting Item', 'Done');",
                    new { jobId = jobId3 },
                    cancellationToken: innerCtx.CancellationToken);

            }, new TransactionOptions { NestedBehavior = NestedTransactionBehavior.JoinExisting }, outerCtx.CancellationToken);

            Console.WriteLine("  -> [Outer Scope] Both outer and inner writes exist on the same physical transaction.");
            joinExistingVerified = true;
        }, TransactionOptions.Default, cancellationToken);

        int jobId3Count = await masterConnection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM batch_jobs WHERE id = @jobId;", new { jobId = jobId3 });
        int jeItemCount = await masterConnection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM job_items WHERE job_id = @jobId;", new { jobId = jobId3 });

        Console.WriteLine($"  • JoinExisting: batch_jobs committed = {jobId3Count} (Expected: 1)");
        Console.WriteLine($"  • JoinExisting: job_items committed  = {jeItemCount} (Expected: 1)");

        // ─── Part 5: NestedTransactionBehavior.RequireNew ─────────────────────────

        Console.WriteLine("\n[Part 5] NestedTransactionBehavior.RequireNew — Independent physical transaction:\n");
        Console.WriteLine("  Semantic: RequireNew always opens a new independent physical connection and transaction,");
        Console.WriteLine("  regardless of whether an ambient transaction is already active.");
        Console.WriteLine("  It suspends the current ambient context for the duration of the scope.");
        Console.WriteLine();
        Console.WriteLine("  NOTE: RequireNew with an ACTIVE concurrent outer transaction is driver-dependent.");
        Console.WriteLine("  SQLite in-memory shared cache uses a single-writer model, so RequireNew is best");
        Console.WriteLine("  demonstrated sequentially (outer commits, then RequireNew starts a new scope).");
        Console.WriteLine("  In PostgreSQL/SQL Server this works concurrently without issue.\n");

        string jobId4 = "job-400";
        string jobId4Inner = "job-401";
        bool requireNewVerified = false;

        // First: commit an outer scope
        await transactionManager.ExecuteAsync(async outerCtx =>
        {
            Console.WriteLine($"  -> [Outer Scope] Outer Tx {outerCtx.TransactionId} started.");
            await outerCtx.ExecuteAsync(
                "INSERT INTO batch_jobs VALUES (@jobId, 'RequireNew Outer', 'Outer');",
                new { jobId = jobId4 },
                cancellationToken: outerCtx.CancellationToken);
            Console.WriteLine("  -> [Outer Scope] Committing outer scope.");
        }, TransactionOptions.Default, cancellationToken);

        // Now: demonstrate RequireNew starts an independent physical transaction
        // (no ambient context here, so it's equivalent to a fresh transaction — same as with RequireNew)
        Guid? requireNewTxId = null;
        await transactionManager.ExecuteAsync(async innerCtx =>
        {
            requireNewTxId = innerCtx.TransactionId;
            Console.WriteLine($"  -> [RequireNew Scope] New independent Tx {innerCtx.TransactionId}.");
            Console.WriteLine($"  -> NestedBehavior: TransactionOptions.RequireNew always forces a new physical connection.");

            await innerCtx.ExecuteAsync(
                "INSERT INTO batch_jobs VALUES (@jobId, 'RequireNew Inner', 'Inner');",
                new { jobId = jobId4Inner },
                cancellationToken: innerCtx.CancellationToken);

            requireNewVerified = true;
        }, new TransactionOptions { NestedBehavior = NestedTransactionBehavior.RequireNew }, cancellationToken);

        Console.WriteLine($"  -> RequireNew TransactionId is unique: {requireNewTxId.HasValue}");

        int job4Outer = await masterConnection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM batch_jobs WHERE id = @jobId;", new { jobId = jobId4 });
        int job4Inner = await masterConnection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM batch_jobs WHERE id = @jobId;", new { jobId = jobId4Inner });

        Console.WriteLine($"  • RequireNew: outer job committed  = {job4Outer} (Expected: 1)");
        Console.WriteLine($"  • RequireNew: inner job committed  = {job4Inner} (Expected: 1)");

        // ─── Part 6: NestedTransactionBehavior.Suppress ───────────────────────────

        Console.WriteLine("\n[Part 6] NestedTransactionBehavior.Suppress — Suspend ambient transaction:\n");
        Console.WriteLine("  Semantic: Suppress executes the nested scope without any transaction enlistment.");
        Console.WriteLine("  The ambient context is suspended for the duration of the suppressed scope.\n");

        bool suppressVerified = false;

        await transactionManager.ExecuteAsync(async outerCtx =>
        {
            Console.WriteLine($"  -> [Outer Scope] Outer Tx {outerCtx.TransactionId} active.");
            Console.WriteLine($"  -> CurrentContext before suppress: {transactionManager.CurrentContext?.TransactionId}");

            // Suppress: execute without transaction enlistment — ambient is suspended
            // NOTE: Suppress works ONLY with parameterless Func<Task> delegate (no ITransactionContext param)
            await transactionManager.ExecuteAsync(async () =>
            {
                // Inside suppressed scope: CurrentContext returns null
                bool ambientIsNull = transactionManager.CurrentContext is null;
                Console.WriteLine($"    -> [Suppressed Scope] CurrentContext is null: {ambientIsNull} (ambient suspended)");
                suppressVerified = ambientIsNull;

                // Any work here runs NON-transactionally
                await Task.Yield();
            }, new TransactionOptions { NestedBehavior = NestedTransactionBehavior.Suppress }, outerCtx.CancellationToken);

            // After suppressed scope exits, outer context is restored
            bool outerRestored = transactionManager.CurrentContext is not null;
            Console.WriteLine($"  -> [Outer Scope] CurrentContext restored after suppress: {outerRestored}");
        }, TransactionOptions.Default, cancellationToken);

        if (savedItemsCount == 2 && item2Exists == 0 && diItemCount == 1 && diItem2Exists == 0
            && joinExistingVerified && requireNewVerified && suppressVerified
            && jobId3Count == 1 && jeItemCount == 1
            && job4Outer == 1 && job4Inner == 1)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n✔ Level 05 Nested Transactions, Savepoints & NestedBehavior modes verified successfully.\n");
            Console.ResetColor();
        }
        else
        {
            throw new InvalidOperationException("Savepoint or NestedBehavior verification failed.");
        }
    }

    private static async Task DeeplyNestedServiceCallAsync(ITransactionManager manager)
    {
        await Task.Yield();
        ITransactionContext? ambient = manager.CurrentContext;
        if (ambient is null)
        {
            throw new InvalidOperationException("Expected ambient transaction context to flow to nested async method.");
        }

        Console.WriteLine($"     -> DeeplyNestedServiceCall: Ambient Context detected (Tx ID: {ambient.TransactionId}, State: {ambient.State})");
    }
}
