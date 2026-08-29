# Level 06: Error Handling, Commit Ambiguity & Error Classifiers

> **Level:** 06 | **Category:** Resilience | **Executable Reference:** [`Level6_ErrorHandling.cs`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/samples/Showcase/Levels/Level6_ErrorHandling.cs)

---

## 1. Exception Hierarchy

`EricksonLopez.Transaction` defines a specialized, strongly typed exception hierarchy under [`EricksonLopez.Transaction.Exceptions`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/src/EricksonLopez.Transaction.Abstractions/Exceptions/):

```text
TransactionException (Base)
  ├── TransactionStateException        (Attempted illegal state transition, e.g. Commit on Disposed)
  ├── TransactionCommitException       (Physical commit failed; contains IsAmbiguous flag)
  ├── TransactionRollbackException     (Teardown rollback failed due to connection drop)
  └── TransactionTimeoutException      (Operation exceeded configured TransactionOptions.Timeout)
```

---

## 2. Commit Ambiguity Handling (`IsAmbiguous`)

When `CommitAsync` fails due to a network drop, TCP timeout, or proxy reset, the database engine may have already written the transaction to disk. Treating this failure as a guaranteed rollback results in **duplicate charges and split-brain corruption**.

```csharp
try
{
    await txManager.ExecuteAsync(async context =>
    {
        await idempotencyStore.RecordKeyAsync(requestId, context);
        await paymentService.ChargeCardAsync(details, context);
    }, TransactionOptions.Default, ct);
}
catch (TransactionCommitException ex) when (ex.IsAmbiguous)
{
    // The transaction MAY have succeeded on the database.
    // Query Idempotency Store or target entity to verify state before retrying:
    bool committed = await idempotencyStore.HasKeyAsync(requestId);
    if (!committed)
    {
        throw; // Safe to retry outer operation
    }
}
```

---

## 3. Multi-Dialect Error Classifiers for Outer Resilience

Outer resilience policies (e.g. Polly) use the 6 dialect error classifiers to identify transient deadlock and serialization failures:

| Engine | Error Classifier | Deadlock Detection | Serialization Conflict |
|---|---|---|---|
| **PostgreSQL** | `PostgreSqlErrorClassifier` | SQLSTATE `40P01` | SQLSTATE `40001` |
| **SQL Server** | `SqlServerErrorClassifier` | Error Number `1205` | Error Numbers `3960`, `3961` |
| **MySQL** | `MySqlErrorClassifier` | Error Number `1213` | Error Number `1205` (Lock Timeout) |
| **MariaDB** | `MariaDbErrorClassifier` | Error Number `1213` | Error Number `1205` (Lock Timeout) |
| **Oracle** | `OracleErrorClassifier` | `ORA-00060` | `ORA-08177` |
| **SQLite** | `SqliteErrorClassifier` | `SQLITE_BUSY` (5) | `SQLITE_LOCKED` (6) |
