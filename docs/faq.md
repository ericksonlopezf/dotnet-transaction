# Frequently Asked Questions (FAQ)

> **Answers to common architectural, operational, and development questions regarding EricksonLopez.Transaction.**

---

### Q1: How does `EricksonLopez.Transaction` differ from `System.Transactions.TransactionScope`?

| Feature | `System.Transactions.TransactionScope` | `EricksonLopez.Transaction` |
|---|---|---|
| **Underlying Primitive** | Distributed Transaction Coordinator (MSDTC) & OLE Transactions | Standard ADO.NET `DbConnection` & `DbTransaction` |
| **Nested Execution** | Escalates to distributed 2PC or requires suppression | Deterministic database savepoints (`SAVEPOINT`) |
| **Async Context Switching** | High allocation, thread-switching hazards | Zero-allocation `AsyncLocal<ITransactionContext?>` |
| **Native AOT & Trimming** | Unconstrained reflection, dynamic COM interop | 100% Native AOT compliant, zero reflection on hot path |
| **Functional Result Monad**| Ignorant: commits on `Result.Failure` | Native `ExecuteResultAsync`: rolls back on failure |
| **Commit Ambiguity** | Generic `TransactionException` | Explicit `TransactionCommitException.IsAmbiguous` flag |

`TransactionScope` was designed in the .NET Framework 2.0 era for on-premise Windows servers with MSDTC. In modern Linux/Docker containers and cloud environments, distributed 2PC is fragile and discouraged. `EricksonLopez.Transaction` manages single-database boundaries deterministically.

---

### Q2: How does ambient context flow across `await` boundaries?

`EricksonLopez.Transaction` uses .NET's `AsyncLocal<ITransactionContext?>`. When `ExecuteAsync` or `BeginAsync` initiates a transaction, it binds the `ITransactionContext` to the current `ExecutionContext`.

Because `AsyncLocal` automatically flows down the asynchronous call stack:
- Any method called from inside the transactional delegate has access to the current transaction.
- Context is isolated to the execution tree; concurrent requests across different threads cannot access or corrupt each other's transactions.
- Upon exiting `ExecuteAsync` (or disposing `ITransaction`), the ambient context is restored to its previous value (preventing context leaks).

---

### Q3: What happens if a nested method calls `ExecuteAsync` while a transaction is already active?

By default, `TransactionOptions.NestedBehavior` is set to `NestedTransactionBehavior.UseSavepoint`.

1. **Savepoint Creation**: Instead of opening a new physical connection or throwing an exception, `TransactionManager` creates a SQL `SAVEPOINT` on the active connection.
2. **Isolated Partial Rollback**: If the inner delegate throws an exception that is caught by the outer method, only the inner savepoint is rolled back (`ROLLBACK TO SAVEPOINT`). The outer transaction remains active and can proceed to commit.
3. **Other Nested Policies**:
   - `JoinExisting`: Shares the transaction without creating a savepoint (all-or-nothing participation).
   - `RequireNew`: Suspends the ambient transaction and opens an independent physical connection.
   - `Suppress`: Suspends transactional context entirely, running operations autocommit.

---

### Q4: What does `TransactionCommitException.IsAmbiguous` mean and how should I handle it?

During a database commit, two phases occur:
1. Client sends the `COMMIT` command over the TCP socket.
2. Database executes the commit and sends back an acknowledgment packet.

If a network timeout or connection reset occurs between step 1 and step 2:
- The database **may or may not** have successfully committed the transaction on disk.
- If the driver throws a socket exception, `TransactionCommitException` wraps it and marks `IsAmbiguous = true`.

**How to handle it:**
- **Never blindly retry an ambiguous commit**: Retrying might charge a customer twice or duplicate orders.
- **Trigger Idempotency Reconciliation**: Query the database using a unique business key (e.g., `OrderId` or `IdempotencyKey`) to verify if the record was committed before deciding whether to retry.

---

### Q5: Why do I get PostgreSQL `SQLSTATE 25P02: current transaction is aborted, commands ignored until end of transaction block`?

In PostgreSQL, once any SQL query inside a transaction causes an error (e.g., foreign key violation, syntax error, or unique constraint conflict), PostgreSQL marks the entire transaction block as aborted. All subsequent queries within that block will fail with `25P02`.

**Solutions:**
1. **Never retry queries inside an aborted PostgreSQL block**: Wrap retries around the *entire* `ExecuteAsync` boundary using `EricksonLopez.Transaction.Resilience`.
2. **Use Savepoints**: If a query might fail but you want to continue the transaction, execute it inside a nested scope with `NestedTransactionBehavior.UseSavepoint`. Rolling back to the savepoint clears PostgreSQL's aborted state.

---

### Q6: How does `ExecuteResultAsync` prevent accidental commits on functional failure?

In Railway-Oriented Programming (using `EricksonLopez.Result`):
```csharp
var result = await transactionManager.ExecuteResultAsync(async context =>
{
    var validation = Validate(order);
    if (validation.IsFailure)
    {
        return Result<Order>.Failure(validation.Error);
    }
    
    await InsertOrderAsync(context, order);
    return Result<Order>.Success(order);
});
```

Standard `try/catch` coordinators only roll back when an exception is thrown. Because returning `Result.Failure` does not throw an exception, traditional wrappers mistakenly commit the transaction!

`ExecuteResultAsync` inspects `result.IsFailure`:
- If `true`, it immediately executes `RollbackAsync()` and returns the failure.
- If `false`, it executes `CommitAsync()` and returns the success.

---

### Q7: Is `EricksonLopez.Transaction` 100% Native AOT compatible?

**Yes.** All packages in this repository are verified with:
- Zero unconstrained reflection.
- Zero dynamic code emission (`Reflection.Emit`).
- Roslyn compile-time `[LoggerMessage]` source generation.
- Dedicated `EricksonLopez.Transaction.AotSmokeTest` test suite compiled with `<PublishAot>true</PublishAot>`.

*Note: If you use third-party libraries (like older versions of Dapper or EF Core), ensure you configure their respective AOT source generators (e.g., `Dapper.AOT`).*

---

### Q8: Can I enlist multiple databases into a single distributed transaction?

`EricksonLopez.Transaction` explicitly **rejects distributed Two-Phase Commit (2PC / MSDTC)** (see [ADR-012](adr/adr-012-rejection-of-distributed-2pc-transactions.md)).

Distributed transactions over networks introduce high latency, locking bottlenecks, and single points of failure. In cloud and microservice architectures, cross-database consistency should be achieved via:
1. **The Transactional Outbox Pattern** (see [Cookbook Recipe 05](cookbook.md#recipe-05-transactional-outbox-pattern-via-lifecycle-enlistments)).
2. **Saga Orchestration** with compensating transactions.

---

### Q9: What is `TransactionOptions.SanitizeTelemetryMetadata`?

When enabled (`SanitizeTelemetryMetadata = true`), the OpenTelemetry activity instrumentation redacts the following span tags to prevent sensitive identifiers from appearing in distributed tracing platforms (Datadog, Jaeger, Honeycomb):

- **`transaction.id`** — Replaced with `[REDACTED]`.
- **`transaction.name`** — Replaced with `[REDACTED]` (if `TransactionOptions.TransactionName` was set).

> **Important:** This option does **not** redact SQL command text, query parameters, exception messages, or connection string fragments — those are not emitted by the framework's telemetry layer. For SQL parameter scrubbing, configure OpenTelemetry `Processor` or `Sampler` at the application level. See [ADR-033](adr/adr-033-sanitize-telemetry-metadata-pii-redaction-scope.md) for the full scope definition.

---

### Q10: How do I run both EF Core and Dapper in the same transaction?

Both tools can share the exact same `DbConnection` and `DbTransaction`:

```csharp
await transactionManager.ExecuteAsync(async context =>
{
    // 1. Enlist EF Core
    dbContext.EnlistInTransaction(context);
    dbContext.Orders.Add(order);
    await dbContext.SaveChangesAsync();

    // 2. Execute fast Dapper bulk insert on the same transaction
    await context.ExecuteAsync(
        "INSERT INTO order_audit (order_id, action) VALUES (@Id, @Action)",
        new { Id = order.Id, Action = "Created" });
});
```
