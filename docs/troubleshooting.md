# Troubleshooting Guide: EricksonLopez.Transaction

> **Diagnostic workflows, root-cause analyses, and remediation strategies for runtime exceptions, concurrency conflicts, and compile-time warnings.**

---

## 1. Exception Diagnostic Matrix

| Exception Type | Common Root Cause | Diagnostic & Resolution Strategy |
|---|---|---|
| `TransactionCommitException` (`IsAmbiguous = true`) | Network socket drop or driver timeout during the physical `COMMIT` command. | Database state is uncertain. **Do not naively retry**. Query database using an idempotency key to determine if data was persisted. |
| `TransactionCommitException` (`IsAmbiguous = false`) | Pre-commit validation failure, deferred constraint violation, or immediate server reject. | State is confirmed aborted. Safe to retry using a resilience policy. |
| `TransactionPostCommitException` | Database `COMMIT` succeeded, but an `ITransactionEnlistment.AfterCommitAsync` callback threw an unhandled exception. | Database changes **are committed on disk**. Inspect inner exception to resolve faulty enlistment (e.g. message broker unreachable). |
| `TransactionStateException` | Attempting `CommitAsync()` or `RollbackAsync()` on an already committed, rolled-back, or disposed transaction. | Ensure transaction handle is only committed once. Review branching logic to avoid duplicate lifecycle calls. |
| `TransactionRollbackException` | Physical `ROLLBACK` command failed (e.g., database connection forcibly terminated by server). | Connection is dead. Log error and release connection pool slot. |
| Roslyn Diagnostic `ELT001` | Direct manipulation of `DbConnection` in `ITransactionContext` (invoking `Close`, `Dispose`, or `BeginTransaction` on `context.Connection`). | Do not invoke lifecycle methods directly on `context.Connection`; allow `TransactionManager` to coordinate the connection and transaction lifecycle. |

---

## 2. Resolving Commit Ambiguity (`IsAmbiguous = true`)

### Scenario
An application issues a commit for a financial transfer or order placement. A transient network partition occurs before the database driver receives the TCP ACK packet:

```csharp
try
{
    await transactionManager.ExecuteAsync(async context =>
    {
        await InsertOrderAsync(context, order);
    }, cancellationToken);
}
catch (TransactionCommitException ex) when (ex.IsAmbiguous)
{
    // The order MIGHT have been committed, or it MIGHT have been aborted!
    await HandleAmbiguousCommitAsync(order.Id, ex);
}
```

### Remediation Workflow
```text
┌───────────────────────────────────────────────────────────┐
│     Catch TransactionCommitException (IsAmbiguous == true)│
└─────────────────────────────┬─────────────────────────────┘
                              │
┌─────────────────────────────▼─────────────────────────────┐
│  Query Outbox / Domain Table by Idempotency Key / OrderId │
│            (Using a new, independent connection)          │
└─────────────────────────────┬─────────────────────────────┘
                              │
               ┌──────────────┴──────────────┐
               ▼                             ▼
        [Record Exists]               [Record Missing]
               │                             │
    Transaction committed!        Transaction aborted!
    Return success to client      Safe to retry business
    (Do not duplicate insert)     operation from scratch
```

---

## 3. Database Engine Deadlocks & Serialization Conflicts

When concurrent transactions contend for the same rows or table locks, database engines abort one of the transactions.

### Dialect Diagnostic Reference

#### 1. PostgreSQL
- **SQLSTATE 40P01** (`deadlock_detected`): Two processes blocked waiting for locks held by each other.
- **SQLSTATE 40001** (`serialization_failure`): Concurrent update conflict under `Serializable` or `RepeatableRead`.
- **SQLSTATE 25P02** (`in_failed_sql_transaction`): An earlier statement failed; PostgreSQL ignores all subsequent commands until rollback.
  - *Fix*: Do not catch exceptions inside the transaction to retry queries. Roll back to a Savepoint, or wrap `ExecuteAsync` in an outer resilience retry policy.

#### 2. Microsoft SQL Server
- **Error 1205** (`Deadlock victim`): The engine chose this transaction as the deadlock victim.
- **Error 3960 / 3961** (`Snapshot isolation conflict`): Conflict updating a row modified by another transaction since the snapshot started.
  - *Fix*: Retry the entire transaction using `EricksonLopez.Transaction.Resilience`.

#### 3. MySQL & MariaDB
- **Error 1213** (`Deadlock found when trying to get lock`): InnoDB detected a deadlock and rolled back the transaction.
- **Error 1205** (`Lock wait timeout exceeded`): Transaction waited longer than `innodb_lock_wait_timeout`.

#### 4. Oracle Database
- **ORA-00060** (`deadlock detected while waiting for resource`): Circular lock wait condition.

#### 5. SQLite
- **SQLITE_BUSY** (Code 5): Another process holds a write lock on the database file.
  - *Fix*: Enable WAL mode (`PRAGMA journal_mode=WAL;`) and configure a busy timeout (`PRAGMA busy_timeout=5000;`).

---

## 4. Connection Pool Exhaustion & Leaks

### Symptoms
- `TimeoutException: Connection pool timeout reached (e.g., 15 seconds)`.
- Slow API response times degrading into total application paralysis.

### Causes & Solutions

1. **Long-Running Transactions**:
   - *Problem*: Executing external HTTP requests, waiting for message queues, or doing heavy file processing inside `ExecuteAsync`.
   - *Solution*: **Never perform network calls inside a transaction**. Fetch external data *before* entering `ExecuteAsync`, or store outbound payloads in a Transactional Outbox table.

2. **Unenlisted Repositories**:
   - *Problem*: Repository creates its own `new NpgsqlConnection()` rather than using `context.Connection`.
   - *Solution*: Always pass `ITransactionContext` into repository methods or use the ambient context accessor.

3. **Missing Asynchronous Disposal**:
   - *Problem*: Using `transaction.Dispose()` instead of `await transaction.DisposeAsync()`.
   - *Solution*: Always use `await using var tx = await transactionManager.BeginAsync()`.

---

## 5. Roslyn Compile-Time Analyzer Warnings

The `EricksonLopez.Transaction.Analyzers` package enforces transactional correctness during compilation:

### Warning `ELT001`: Uncommitted Transaction Handle

```csharp
// ❌ FAILS ELT001: Transaction is begun but never committed or disposed
public async Task ProcessData()
{
    var tx = await _transactionManager.BeginAsync();
    await DoWork(tx.Context);
    // Warning ELT001: Transaction was not committed or disposed asynchronously
}

// ✅ COMPLIANT: Use await using and explicit CommitAsync
public async Task ProcessData()
{
    await using var tx = await _transactionManager.BeginAsync();
    await DoWork(tx.Context);
    await tx.CommitAsync();
}
```

---

## 6. OpenTelemetry Diagnostic Verification

If traces or metrics do not appear in your APM dashboard:

1. **Verify ActivitySource Name**:
   Register the exact name `"EricksonLopez.Transaction"`:
   ```csharp
   services.AddOpenTelemetry()
       .WithTracing(tracing => tracing
           .AddSource("EricksonLopez.Transaction")
           .AddOtlpExporter())
       .WithMetrics(metrics => metrics
           .AddMeter("EricksonLopez.Transaction")
           .AddOtlpExporter());
   ```

2. **Check Telemetry Sanitization**:
   If `transaction.id` or `transaction.name` appear as `[REDACTED]` in your OTel spans, `TransactionOptions.SanitizeTelemetryMetadata` is set to `true`. This is intentional in production environments to prevent sensitive transaction identifiers from appearing in tracing platforms. Disable it in local development environments to see raw transaction IDs and names in span tags.
