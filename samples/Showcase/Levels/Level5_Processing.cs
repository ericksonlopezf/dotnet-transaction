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
/// Level 05: Nested Transactions, Savepoints &amp; Ambient Context Flow.
/// Demonstrates hierarchical Savepoint semantics for partial rollback and ambient AsyncLocal context propagation.
/// Also demonstrates direct ISavepoint API usage: CreateSavepointAsync, RollbackAsync, ReleaseAsync, Name.
/// </summary>
public sealed class Level5_Processing : ILevel
{
    public int LevelNumber => 5;
    public string Name => "Nested Transactions, Savepoints & Ambient Context Flow";
    public string Description => "Demonstrates hierarchical Savepoint isolation, partial rollback recovery, direct ISavepoint API (CreateSavepointAsync, RollbackAsync, ReleaseAsync), and ambient AsyncLocal transaction propagation.";
    public string Category => "Advanced";

    public async Task RunAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("  LEVEL 05: NESTED TRANSACTIONS, SAVEPOINTS & AMBIENT CONTEXT FLOW");
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

        if (savedItemsCount == 2 && item2Exists == 0 && diItemCount == 1 && diItem2Exists == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n✔ Level 05 Nested Transactions & Savepoints verified successfully.\n");
            Console.ResetColor();
        }
        else
        {
            throw new InvalidOperationException("Savepoint verification failed.");
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
