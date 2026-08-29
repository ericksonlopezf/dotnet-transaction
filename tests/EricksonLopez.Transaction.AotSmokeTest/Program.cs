// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;
using EricksonLopez.Transaction;
using EricksonLopez.Transaction.Diagnostics;
using EricksonLopez.Transaction.Exceptions;
using EricksonLopez.Transaction.MariaDb;
using EricksonLopez.Transaction.MySql;
using EricksonLopez.Transaction.Oracle;
using EricksonLopez.Transaction.PostgreSql;
using EricksonLopez.Transaction.Result;
using EricksonLopez.Transaction.Sqlite;
using EricksonLopez.Transaction.SqlServer;
using EricksonLopez.Transaction.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("=================================================");
Console.WriteLine(" EricksonLopez.Transaction NativeAOT Test Suite ");
Console.WriteLine("=================================================");

int passedTests = 0;

void Assert([DoesNotReturnIf(false)] bool condition, string testName)
{
    if (!condition)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[FAIL] {testName}");
        Console.ResetColor();
        throw new InvalidOperationException($"Assertion failed for: {testName}");
    }

    passedTests++;
    Console.WriteLine($"[PASS] {testName}");
}

// ── 1. TransactionOptions & Enums ──────────────────────────────────────────
Console.WriteLine("\n--- 1. TransactionOptions & Invariants ---");

var defaultOptions = TransactionOptions.Default;
Assert(defaultOptions.IsolationLevel == TransactionIsolationLevel.ReadCommitted, "Default isolation is ReadCommitted");
Assert(defaultOptions.NestedBehavior == NestedTransactionBehavior.UseSavepoint, "Default nested behavior is UseSavepoint");
Assert(!defaultOptions.ReadOnly, "Default is not read-only");

var serializableOptions = TransactionOptions.Serializable;
Assert(serializableOptions.IsolationLevel == TransactionIsolationLevel.Serializable, "Serializable isolation is set");

var timeoutOptions = TransactionOptions.WithTimeout(TimeSpan.FromSeconds(5));
Assert(timeoutOptions.Timeout == TimeSpan.FromSeconds(5), "Timeout option is set");

var readOnlyOptions = TransactionOptions.ReadOnlyMode;
Assert(readOnlyOptions.ReadOnly, "ReadOnly mode is true");

// ── 2. Transaction Exception Hierarchy ────────────────────────────────────
Console.WriteLine("\n--- 2. Transaction Exceptions ---");

var innerTimeout = new TimeoutException("Database connection timeout.");
var commitEx = new TransactionCommitException("Commit failed", innerTimeout, isAmbiguous: true);
Assert(commitEx.IsAmbiguous, "TransactionCommitException marks ambiguous commit failure");
Assert(ReferenceEquals(commitEx.InnerException, innerTimeout), "Inner exception is preserved");

var timeoutEx = new TransactionTimeoutException(TimeSpan.FromSeconds(10));
Assert(timeoutEx.Timeout == TimeSpan.FromSeconds(10), "Timeout exception contains duration");

var stateEx = new TransactionStateException(TransactionState.Committed, "Rollback");
Assert(stateEx.ActualState == TransactionState.Committed, "State exception contains actual state");
Assert(stateEx.AttemptedOperation == "Rollback", "State exception contains attempted operation");

// ── 3. TransactionManager Physical Execution ──────────────────────────────
Console.WriteLine("\n--- 3. TransactionManager Physical Execution ---");

var masterConn = new SqliteConnection("Data Source=aot_test_db;Mode=Memory;Cache=Shared");
masterConn.Open();

var factory = new DelegateDbConnectionFactory(() =>
{
    var conn = new SqliteConnection("Data Source=aot_test_db;Mode=Memory;Cache=Shared");
    conn.Open();
    return conn;
});

// Initialize in-memory database table
using (var initCmd = masterConn.CreateCommand())
{
    initCmd.CommandText = "CREATE TABLE IF NOT EXISTS accounts (id INT PRIMARY KEY, balance DECIMAL);";
    initCmd.ExecuteNonQuery();
}

var manager = new TransactionManager(factory);

// 3a. Successful transaction commit
await manager.ExecuteAsync(async context =>
{
    Assert(context.State == TransactionState.Active, "Context is active during execution");
    Assert(context.Connection is not null, "Connection is attached to context");
    Assert(context.Transaction is not null, "Transaction is attached to context");

    using var cmd = context.Connection.CreateCommand();
    cmd.Transaction = (SqliteTransaction)context.Transaction;
    cmd.CommandText = "INSERT INTO accounts (id, balance) VALUES (1, 500.0);";
    await cmd.ExecuteNonQueryAsync(context.CancellationToken);
});

// Verify committed data
using (var verifyConn = factory.CreateConnection())
{
    using var checkCmd = verifyConn.CreateCommand();
    checkCmd.CommandText = "SELECT balance FROM accounts WHERE id = 1;";
    var balance = Convert.ToDouble(checkCmd.ExecuteScalar());
    Assert(Math.Abs(balance - 500.0) < 0.001, "Account balance committed successfully");
}

// 3b. Automatic rollback on exception
bool exceptionCaught = false;
try
{
    await manager.ExecuteAsync(async context =>
    {
        using var cmd = context.Connection.CreateCommand();
        cmd.Transaction = (SqliteTransaction)context.Transaction;
        cmd.CommandText = "INSERT INTO accounts (id, balance) VALUES (2, 999.0);";
        await cmd.ExecuteNonQueryAsync(context.CancellationToken);

        throw new InvalidOperationException("Simulated business domain failure");
    });
}
catch (InvalidOperationException)
{
    exceptionCaught = true;
}
Assert(exceptionCaught, "Exception propagates from ExecuteAsync");

// Verify rolled back data
using (var verifyConn2 = factory.CreateConnection())
{
    using var checkCmd2 = verifyConn2.CreateCommand();
    checkCmd2.CommandText = "SELECT COUNT(*) FROM accounts WHERE id = 2;";
    long count = (long)(checkCmd2.ExecuteScalar()!);
    Assert(count == 0, "Failed transaction was completely rolled back");
}

// ── 4. Nested Savepoint Execution ─────────────────────────────────────────
Console.WriteLine("\n--- 4. Nested Savepoint Scopes ---");

await manager.ExecuteAsync(async outerContext =>
{
    using (var cmd = outerContext.Connection.CreateCommand())
    {
        cmd.Transaction = (SqliteTransaction)outerContext.Transaction;
        cmd.CommandText = "INSERT INTO accounts (id, balance) VALUES (10, 100.0);";
        await cmd.ExecuteNonQueryAsync(outerContext.CancellationToken);
    }

    // Nested scope with Savepoint
    await manager.ExecuteAsync(async innerContext =>
    {
        using var innerCmd = innerContext.Connection.CreateCommand();
        innerCmd.Transaction = (SqliteTransaction)innerContext.Transaction;
        innerCmd.CommandText = "INSERT INTO accounts (id, balance) VALUES (11, 200.0);";
        await innerCmd.ExecuteNonQueryAsync(innerContext.CancellationToken);
    });
});

using (var verifyNested = factory.CreateConnection())
{
    using var checkNested = verifyNested.CreateCommand();
    checkNested.CommandText = "SELECT COUNT(*) FROM accounts WHERE id IN (10, 11);";
    long nestedCount = (long)(checkNested.ExecuteScalar()!);
    Assert(nestedCount == 2, "Both outer and nested savepoint operations committed successfully");
}

// ── 5. Result Monad Integration ───────────────────────────────────────────
Console.WriteLine("\n--- 5. Result Monad Auto-Rollback ---");

var successResult = await manager.ExecuteResultAsync(async context =>
{
    using var cmd = context.Connection.CreateCommand();
    cmd.Transaction = (SqliteTransaction)context.Transaction;
    cmd.CommandText = "INSERT INTO accounts (id, balance) VALUES (20, 1000.0);";
    await cmd.ExecuteNonQueryAsync(context.CancellationToken);

    return EricksonLopez.Result.Result.Success(20);
});
Assert(successResult.IsSuccess && successResult.Value == 20, "ExecuteResultAsync commits on Result.Success");

var failureResult = await manager.ExecuteResultAsync(async context =>
{
    using var cmd = context.Connection.CreateCommand();
    cmd.Transaction = (SqliteTransaction)context.Transaction;
    cmd.CommandText = "INSERT INTO accounts (id, balance) VALUES (21, 2000.0);";
    await cmd.ExecuteNonQueryAsync(context.CancellationToken);

    return EricksonLopez.Result.Result.Failure<int>(Error.Validation("Acc.Invalid", "Invalid account"));
});
Assert(failureResult.IsFailure, "ExecuteResultAsync returns Result.Failure");

using (var verifyResultRollback = factory.CreateConnection())
{
    using var checkResult = verifyResultRollback.CreateCommand();
    checkResult.CommandText = "SELECT COUNT(*) FROM accounts WHERE id = 21;";
    long failedCount = (long)(checkResult.ExecuteScalar()!);
    Assert(failedCount == 0, "Result.Failure triggered automatic rollback of database transaction");
}

// ── 6. Dialect Error Classifiers ──────────────────────────────────────────
Console.WriteLine("\n--- 6. Dialect Error Classifiers ---");

var timeoutExGeneric = new TimeoutException("Connection dropped");
Assert(PostgreSqlErrorClassifier.IsTransient(timeoutExGeneric), "PostgreSqlErrorClassifier detects transient timeout");
Assert(SqlServerErrorClassifier.IsTransient(timeoutExGeneric), "SqlServerErrorClassifier detects transient timeout");
Assert(MySqlErrorClassifier.IsTransient(timeoutExGeneric), "MySqlErrorClassifier detects transient timeout");
Assert(MariaDbErrorClassifier.IsTransient(timeoutExGeneric), "MariaDbErrorClassifier detects transient timeout");
Assert(OracleErrorClassifier.IsTransient(timeoutExGeneric), "OracleErrorClassifier detects transient timeout");
Assert(SqliteErrorClassifier.IsTransient(timeoutExGeneric), "SqliteErrorClassifier detects transient timeout");

// ── 7. OpenTelemetry Diagnostics ──────────────────────────────────────────
Console.WriteLine("\n--- 7. Diagnostics Verification ---");

TransactionDiagnostics.RecordStarted(TransactionIsolationLevel.ReadCommitted);
TransactionDiagnostics.RecordCommitted(TransactionIsolationLevel.ReadCommitted, 15.2);
TransactionDiagnostics.RecordRolledBack(TransactionIsolationLevel.ReadCommitted, 15.2);
TransactionDiagnostics.RecordFailed(TransactionIsolationLevel.ReadCommitted, 15.2, "Timeout");
TransactionDiagnostics.RecordSavepointCreated();
TransactionDiagnostics.RecordSavepointRolledBack();
TransactionDiagnostics.RecordSavepointReleased();

Assert(TransactionDiagnostics.Meter.Name == "EricksonLopez.Transaction", "Meter name is EricksonLopez.Transaction");
Assert(TransactionDiagnostics.ActivitySource.Name == "EricksonLopez.Transaction", "ActivitySource name is EricksonLopez.Transaction");

// ── 8. In-Memory Testing Doubles ──────────────────────────────────────────
Console.WriteLine("\n--- 8. Testing Doubles (FakeTransactionManager) ---");

var fakeManager = new FakeTransactionManager();
await fakeManager.ExecuteAsync(async context =>
{
    Assert(context.State == TransactionState.Active, "Fake context is active");
    await Task.Yield();
});

Assert(fakeManager.StartedTransactions.Count == 1, "FakeTransactionManager tracked transaction");
Assert(fakeManager.StartedTransactions[0].CommitCount == 1, "FakeTransaction tracked commit");
Assert(fakeManager.StartedTransactions[0].RollbackCount == 0, "FakeTransaction tracked zero rollbacks");

// ── 9. Suppressed Scope & Parameterless ExecuteAsync ─────────────────────────
Console.WriteLine("\n--- 9. Suppressed Scope & Parameterless Overloads ---");

await manager.ExecuteAsync(async () =>
{
    Assert(manager.CurrentContext is not null, "Ambient context is active inside parameterless ExecuteAsync");
    await Task.CompletedTask;
});

await manager.ExecuteAsync(async outer =>
{
    var suppressOpts = new TransactionOptions { NestedBehavior = NestedTransactionBehavior.Suppress };
    await using (var suppressed = await manager.BeginAsync(suppressOpts))
    {
        Assert(manager.CurrentContext is null, "Ambient context is suspended inside Suppressed scope");
    }

    Assert(ReferenceEquals(manager.CurrentContext, outer), "Ambient context is restored after Suppressed scope");
});

Console.WriteLine("\n=================================================");
Console.WriteLine($" ALL {passedTests} NATIVE AOT SUITE TESTS PASSED SUCCESSFULLY! ");
Console.WriteLine("=== AOT Validator: OK ===");
Console.WriteLine("=================================================");

return 0;
