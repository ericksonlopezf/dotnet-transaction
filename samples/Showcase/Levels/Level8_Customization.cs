// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.Transaction.Dapper;
using EricksonLopez.Transaction.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Transaction.Showcase.Levels;

/// <summary>
/// Level 08: Extensibility, Custom Enlistments &amp; In-Memory Test Doubles.
/// Demonstrates custom connection factories (both async and sync overloads), all ITransactionEnlistment
/// lifecycle hooks, and comprehensive unit testing with FakeTransactionManager, FakeTransaction,
/// and FakeTransactionContext public API.
/// <para>
/// IMPORTANT — OnExceptionAsync semantics: This hook fires when CommitAsync or RollbackAsync ITSELF throws,
/// NOT when user operation code throws. User exceptions cause AfterRollbackAsync (auto-rollback via DisposeAsync).
/// </para>
/// </summary>
public sealed class Level8_Customization : ILevel
{
    public int LevelNumber => 8;
    public string Name => "Extensibility, Custom Enlistments & In-Memory Test Doubles";
    public string Description => "Demonstrates DelegateDbConnectionFactory (async & sync overloads), IDbConnectionFactory.CreateConnection(), all 4 ITransactionEnlistment hooks (BeforeCommitAsync, AfterCommitAsync, AfterRollbackAsync, OnExceptionAsync), and FakeTransactionManager/FakeTransaction/FakeTransactionContext public API.";
    public string Category => "Extensibility";

    public async Task RunAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("  LEVEL 08: EXTENSIBILITY, CUSTOM ENLISTMENTS & TEST DOUBLES");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        // ─── 1. DelegateDbConnectionFactory — Async Constructor Overload ─────────

        Console.WriteLine("[1] DelegateDbConnectionFactory — Async Constructor (Func<CancellationToken, ValueTask<DbConnection>>):\n");

        // Correct signature: Func<CancellationToken, ValueTask<DbConnection>>
        IDbConnectionFactory asyncFactory = new DelegateDbConnectionFactory(async ct =>
        {
            var conn = new SqliteConnection("Data Source=customization;Mode=Memory;Cache=Shared");
            await conn.OpenAsync(ct);
            return (DbConnection)conn; // ValueTask<DbConnection> is satisfied by Task<DbConnection> via implicit cast
        });

        DbConnection openedConn = await asyncFactory.CreateConnectionAsync(cancellationToken);
        await openedConn.ExecuteAsync("CREATE TABLE IF NOT EXISTS audit_logs (id TEXT PRIMARY KEY, message TEXT NOT NULL);");
        Console.WriteLine("  -> async DelegateDbConnectionFactory.CreateConnectionAsync() returned open DbConnection OK.");

        // ─── 2. DelegateDbConnectionFactory — Sync Constructor Overload ──────────

        Console.WriteLine("\n[2] DelegateDbConnectionFactory — Sync Constructor (Func<DbConnection>):\n");

        IDbConnectionFactory syncFactory = new DelegateDbConnectionFactory(
            () => new SqliteConnection("Data Source=customization;Mode=Memory;Cache=Shared"));

        // IDbConnectionFactory.CreateConnection() — synchronous method
        DbConnection syncConn = syncFactory.CreateConnection();
        Console.WriteLine($"  -> syncFactory.CreateConnection() returned: {syncConn.GetType().Name} (State: {syncConn.State})");
        Console.WriteLine("  -> CreateConnection() returns closed connection; caller opens it explicitly.");

        // IDbConnectionFactory.CreateConnectionAsync() — also works with sync factory
        DbConnection syncConnOpened = await syncFactory.CreateConnectionAsync(cancellationToken);
        Console.WriteLine($"  -> syncFactory.CreateConnectionAsync() returned: {syncConnOpened.GetType().Name} (State: {syncConnOpened.State})\n");
        syncConnOpened.Dispose();

        // ─── 3. ITransactionEnlistment — All 4 Lifecycle Hooks ──────────────────

        Console.WriteLine("[3] ITransactionEnlistment — All 4 Lifecycle Hooks:\n");
        Console.WriteLine("  Hooks: BeforeCommitAsync, AfterCommitAsync, AfterRollbackAsync, OnExceptionAsync\n");

        var enlistmentTracker = new AuditEnlistment();

        var services = new ServiceCollection();
        services.AddTransaction(_ => new SqliteConnection("Data Source=customization;Mode=Memory;Cache=Shared"));
        using ServiceProvider localProvider = services.BuildServiceProvider();
        ITransactionManager manager = localProvider.GetRequiredService<ITransactionManager>();

        // 3a. Commit path — exercises BeforeCommitAsync and AfterCommitAsync
        Console.WriteLine("  [3a] Commit Path (BeforeCommitAsync + AfterCommitAsync):");
        await manager.ExecuteAsync(async context =>
        {
            context.Enlist(enlistmentTracker);

            await context.ExecuteAsync(
                "INSERT INTO audit_logs VALUES ('log-1', 'User logged in successfully.');",
                cancellationToken: context.CancellationToken);

            Console.WriteLine("  -> Executed INSERT. Enlistment attached. Committing...");
        }, TransactionOptions.Default, cancellationToken);

        Console.WriteLine($"  -> BeforeCommitAsync called: {enlistmentTracker.BeforeCommitCalled}");
        Console.WriteLine($"  -> AfterCommitAsync called:  {enlistmentTracker.AfterCommitCalled}");
        Console.WriteLine($"  -> AfterRollbackAsync called: {enlistmentTracker.AfterRollbackCalled}");
        Console.WriteLine($"  -> OnExceptionAsync called:   {enlistmentTracker.OnExceptionCalled}\n");

        // 3b. Exception path — exercises AfterRollbackAsync (user code throws, auto-rollback via DisposeAsync)
        // NOTE: OnExceptionAsync is NOT called here — it only fires if CommitAsync or RollbackAsync itself throws.
        // User code exceptions cause automatic rollback via DisposeAsync, which triggers AfterRollbackAsync.
        Console.WriteLine("  [3b] User Exception Path → AfterRollbackAsync (auto-rollback on user code failure):");
        Console.WriteLine("       NOTE: OnExceptionAsync is NOT triggered by user code exceptions.");
        Console.WriteLine("       OnExceptionAsync fires only when CommitAsync or RollbackAsync itself throws.");
        var rollbackEnlistment = new AuditEnlistment();

        try
        {
            await manager.ExecuteAsync(async context =>
            {
                context.Enlist(rollbackEnlistment);

                await context.ExecuteAsync(
                    "INSERT INTO audit_logs VALUES ('log-2', 'Partial work before exception.');",
                    cancellationToken: context.CancellationToken);

                throw new InvalidOperationException("Simulated business logic failure.");
            }, TransactionOptions.Default, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"  -> Caught expected exception: {ex.Message}");
        }

        Console.WriteLine($"  -> BeforeCommitAsync called: {rollbackEnlistment.BeforeCommitCalled}  (false — commit never reached)");
        Console.WriteLine($"  -> AfterCommitAsync called:  {rollbackEnlistment.AfterCommitCalled}  (false — no commit)");
        Console.WriteLine($"  -> AfterRollbackAsync called: {rollbackEnlistment.AfterRollbackCalled}  (TRUE — auto-rollback on dispose)");
        Console.WriteLine($"  -> OnExceptionAsync called:   {rollbackEnlistment.OnExceptionCalled}  (false — user exceptions do NOT trigger this hook)");
        Console.WriteLine();

        // 3c. OnExceptionAsync path — Interface Contract Demonstration
        // OnExceptionAsync is fired inside PhysicalTransaction.CommitAsync's catch block.
        // FakeTransactionManager is a lightweight test double: it does NOT replay real hook logic.
        // To trigger OnExceptionAsync: use BeginAsync() directly and call CommitAsync() on a FakeTransaction
        // with ExceptionToThrowOnCommit set, then manually invoke the hook to demonstrate the interface contract.
        Console.WriteLine("  [3c] OnExceptionAsync — Interface Contract Demonstration:");
        Console.WriteLine("       Trigger: CommitAsync or RollbackAsync itself throws (NOT user operation code).");
        Console.WriteLine("       Real scenario: PhysicalTransaction.CommitAsync catches the commit exception and");
        Console.WriteLine("       calls context.ExecuteOnExceptionHooksAsync(ex) before re-throwing as TransactionCommitException.");
        Console.WriteLine("       FakeTransactionManager is a test double and does not replay hook dispatch logic.\n");

        // Demonstrate the OnExceptionAsync interface method directly on the AuditEnlistment:
        var onExceptionEnlistment = new AuditEnlistment();
        var demoCtx = new FakeTransactionContext(isolationLevel: TransactionIsolationLevel.ReadCommitted);
        var demoException = new TimeoutException("Simulated commit-phase DB timeout.");

        // Calling OnExceptionAsync directly as it would be invoked by the real PhysicalTransaction engine:
        await onExceptionEnlistment.OnExceptionAsync(demoCtx, demoException, CancellationToken.None);

        Console.WriteLine($"  -> OnExceptionAsync directly invoked: Called={onExceptionEnlistment.OnExceptionCalled}");
        Console.WriteLine($"  -> Exception type captured: '{onExceptionEnlistment.LastExceptionType}'");
        Console.WriteLine($"  -> In production: real engine calls this when CommitAsync throws (e.g., network drop).\n");

        // ─── 4. FakeTransactionManager — Complete Public API ─────────────────────

        Console.WriteLine("[4] FakeTransactionManager — Complete Public API Demonstration:\n");

        // 4a. Basic commit tracking
        var fakeManager = new FakeTransactionManager();
        var billingService = new BillingService(fakeManager);
        await billingService.ChargeCustomerAsync("CUST-99", 150.0m);

        FakeTransaction firstTx = fakeManager.StartedTransactions[0];
        Console.WriteLine($"  [4a] Basic Commit Tracking:");
        Console.WriteLine($"       fakeManager.StartedTransactions.Count = {fakeManager.StartedTransactions.Count} (Expected: 1)");
        Console.WriteLine($"       firstTx.CommitCount  = {firstTx.CommitCount}   (Expected: 1)");
        Console.WriteLine($"       firstTx.RollbackCount = {firstTx.RollbackCount} (Expected: 0)");
        Console.WriteLine($"       firstTx.IsDisposed   = {firstTx.IsDisposed}  (Expected: true — await using disposed it)");
        Console.WriteLine($"       firstTx.State        = {firstTx.State}");
        Console.WriteLine($"       firstTx.TransactionId = {firstTx.TransactionId}");
        Console.WriteLine($"       firstTx.Context (FakeTransactionContext).IsolationLevel = {firstTx.Context.IsolationLevel}\n");

        // 4b. FakeTransactionContext — public API including CreatedSavepoints
        Console.WriteLine("  [4b] FakeTransactionContext — CreatedSavepoints:");
        var fakeCtx = new FakeTransactionContext(isolationLevel: TransactionIsolationLevel.Serializable);
        ISavepoint sp1 = await fakeCtx.CreateSavepointAsync("sp_test_1", CancellationToken.None);
        ISavepoint sp2 = await fakeCtx.CreateSavepointAsync("sp_test_2", CancellationToken.None);

        Console.WriteLine($"       fakeCtx.TransactionId:   {fakeCtx.TransactionId}");
        Console.WriteLine($"       fakeCtx.State:           {fakeCtx.State}");
        Console.WriteLine($"       fakeCtx.IsolationLevel:  {fakeCtx.IsolationLevel}");
        Console.WriteLine($"       fakeCtx.CreatedSavepoints.Count = {fakeCtx.CreatedSavepoints.Count} (Expected: 2)");
        Console.WriteLine($"       fakeCtx.CreatedSavepoints[0] = '{fakeCtx.CreatedSavepoints[0]}'");
        Console.WriteLine($"       fakeCtx.CreatedSavepoints[1] = '{fakeCtx.CreatedSavepoints[1]}'");
        Console.WriteLine($"       sp1.Name = '{sp1.Name}', sp2.Name = '{sp2.Name}'\n");

        // 4c. Enlistments on FakeTransactionContext
        var fakeEnlistment = new AuditEnlistment();
        fakeCtx.Enlist(fakeEnlistment);
        Console.WriteLine($"       fakeCtx.Enlistments.Count after Enlist() = {fakeCtx.Enlistments.Count} (Expected: 1)\n");

        // 4d. FakeTransaction.ExceptionToThrowOnRollback
        Console.WriteLine("  [4c] FakeTransaction.ExceptionToThrowOnRollback:");
        var fakeManagerWithRollbackEx = new FakeTransactionManager();
        try
        {
            await using ITransaction txHandle = await fakeManagerWithRollbackEx.BeginAsync(cancellationToken: cancellationToken);
            FakeTransaction fakeTx = fakeManagerWithRollbackEx.StartedTransactions[0];
            fakeTx.ExceptionToThrowOnRollback = new InvalidOperationException("Simulated rollback failure.");

            await txHandle.RollbackAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"       Caught ExceptionToThrowOnRollback: '{ex.Message}'");
        }

        // 4e. FakeTransactionManager.CurrentContext settable
        Console.WriteLine("\n  [4d] FakeTransactionManager.CurrentContext (settable):");
        var fakeManagerCtx = new FakeTransactionManager();
        var customCtx = new FakeTransactionContext(Guid.NewGuid(), TransactionIsolationLevel.Snapshot);
        fakeManagerCtx.CurrentContext = customCtx;
        Console.WriteLine($"       fakeManagerCtx.CurrentContext?.IsolationLevel = {fakeManagerCtx.CurrentContext?.IsolationLevel}");

        // 4f. ExceptionToThrowOnCommit
        Console.WriteLine("\n  [4e] FakeTransactionManager.ExceptionToThrowOnCommit:");
        var fakeManagerCommitEx = new FakeTransactionManager
        {
            ExceptionToThrowOnCommit = new TimeoutException("Simulated DB timeout during commit.")
        };

        try
        {
            await fakeManagerCommitEx.ExecuteAsync(_ => Task.CompletedTask, cancellationToken: cancellationToken);
        }
        catch (TimeoutException ex)
        {
            Console.WriteLine($"       Caught ExceptionToThrowOnCommit: '{ex.Message}'");
        }

        bool allVerified = enlistmentTracker.BeforeCommitCalled
            && enlistmentTracker.AfterCommitCalled
            // rollbackEnlistment: user exception causes auto-rollback (AfterRollbackAsync), NOT OnExceptionAsync
            && rollbackEnlistment.AfterRollbackCalled
            && !rollbackEnlistment.OnExceptionCalled // Verified: user exceptions do NOT trigger OnExceptionAsync
            && fakeManager.StartedTransactions.Count == 1
            && firstTx.CommitCount == 1
            && fakeCtx.CreatedSavepoints.Count == 2;

        if (allVerified)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n✔ Level 08 Extensibility & Test Doubles verified successfully.\n");
            Console.ResetColor();
        }
        else
        {
            throw new InvalidOperationException("Customization verification failed.");
        }
    }

    /// <summary>
    /// Demonstration implementation of ITransactionEnlistment covering all 4 interface methods.
    /// </summary>
    private sealed class AuditEnlistment : ITransactionEnlistment
    {
        public bool BeforeCommitCalled { get; private set; }
        public bool AfterCommitCalled { get; private set; }
        public bool AfterRollbackCalled { get; private set; }
        public bool OnExceptionCalled { get; private set; }
        public string? LastExceptionType { get; private set; }

        // Hook 1: Executes before physical commit — flush outbox, validate invariants
        public Task BeforeCommitAsync(ITransactionContext context, CancellationToken cancellationToken = default)
        {
            BeforeCommitCalled = true;
            Console.WriteLine("     [Hook-1] BeforeCommitAsync: Flushing pre-commit memory buffers and outbox...");
            return Task.CompletedTask;
        }

        // Hook 2: Executes after successful physical commit — notify background workers
        public Task AfterCommitAsync(ITransactionContext context, CancellationToken cancellationToken = default)
        {
            AfterCommitCalled = true;
            Console.WriteLine("     [Hook-2] AfterCommitAsync: Transaction committed. Notifying downstream services...");
            return Task.CompletedTask;
        }

        // Hook 3: Executes after transaction rollback — clear dirty state
        public Task AfterRollbackAsync(ITransactionContext context, CancellationToken cancellationToken = default)
        {
            AfterRollbackCalled = true;
            Console.WriteLine("     [Hook-3] AfterRollbackAsync: Transaction rolled back. Clearing dirty aggregate state...");
            return Task.CompletedTask;
        }

        // Hook 4: Executes when an exception occurs during execution or commit phase
        public Task OnExceptionAsync(ITransactionContext context, Exception exception, CancellationToken cancellationToken = default)
        {
            OnExceptionCalled = true;
            LastExceptionType = exception.GetType().Name;
            Console.WriteLine($"     [Hook-4] OnExceptionAsync: Exception intercepted ({exception.GetType().Name}): {exception.Message}");
            Console.WriteLine("              -> Use this hook to log, emit metrics, or alert monitoring systems.");
            return Task.CompletedTask;
        }
    }

    private sealed class BillingService
    {
        private readonly ITransactionManager _transactionManager;

        public BillingService(ITransactionManager transactionManager)
        {
            _transactionManager = transactionManager;
        }

        public async Task ChargeCustomerAsync(string customerId, decimal amount)
        {
            await _transactionManager.ExecuteAsync(async context =>
            {
                // Domain logic without physical DB coupling in unit test
                await Task.Yield();
            });
        }
    }
}
