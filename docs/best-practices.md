# Best Practices & Anti-Patterns: EricksonLopez.Transaction

> **Architectural rules, operational invariants, and anti-pattern rejections for Staff Engineers and Software Architects.**

---

## 1. Core Architectural Rules

### Rule 1: Transaction Ownership Belongs to the Application Layer
- **DO**: Initiate transactions in Application Services, Use Case Handlers, or Mediator Pipeline Behaviors (`ITransactionManager.ExecuteAsync`).
- **DON'T**: Begin transactions inside Repositories or Domain Entities. Repositories must remain focused purely on executing data access commands using the ambient `ITransactionContext`.

```text
┌─────────────────────────────────────────────────────────────┐
│  API / UI Layer (Controllers, Endpoints, Consumers)        │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│  Application Layer (Use Cases, Mediator Handlers)           │
│  ⭐ OWNS TRANSACTION BOUNDARY (ITransactionManager)         │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│  Domain & Persistence Layer (Repositories, DB Contexts)     │
│  Receives ITransactionContext; executes SQL on active tx    │
└─────────────────────────────────────────────────────────────┘
```

---

### Rule 2: Keep Transactions Short, Atomic, and In-Memory
- **DO**: Prepare data, validate business invariants, and compute payloads *before* opening the database transaction.
- **DON'T**: Perform external HTTP API calls, publish messages directly to a remote broker (RabbitMQ/Kafka), or execute heavy disk/CPU work inside an active database transaction.
- **Why**: Holding database locks while waiting on external network I/O exhausts database connection pools and causes catastrophic application-wide cascading deadlocks.

```csharp
// ❌ WRONG: HTTP call inside database transaction
await transactionManager.ExecuteAsync(async context =>
{
    await repository.InsertOrderAsync(context, order);
    
    // Disastrous: If payment gateway takes 5s or times out, DB lock is held!
    await paymentGateway.ChargeCreditCardAsync(order.Total);
});

// ✅ CORRECT: Charge payment first (or use Outbox/Sagas), then transact
var chargeResult = await paymentGateway.ChargeCreditCardAsync(order.Total);
if (chargeResult.IsSuccess)
{
    await transactionManager.ExecuteAsync(async context =>
    {
        await repository.InsertOrderAsync(context, order);
        await repository.RecordPaymentAsync(context, chargeResult.TransactionId);
    });
}
```

---

### Rule 3: Use Savepoints for Partial Recoverable Operations
When an optional or secondary operation might fail without invalidating the core business transaction:
- **DO**: Wrap the secondary operation in a nested `ExecuteAsync` scope with `NestedTransactionBehavior.UseSavepoint`.
- **Catch** the exception in the outer scope and log/handle it gracefully. The outer transaction remains healthy and committable.

---

### Rule 4: Railway-Oriented Programming with `ExecuteResultAsync`
When using `EricksonLopez.Result`:
- **DO**: Use `transactionManager.ExecuteResultAsync()`.
- **Why**: Returning `Result.Failure` automatically executes a database rollback without having to throw or catch exceptions.

---

### Rule 5: Resilience & Retry Wrap the Outer Transaction Boundary
- **DO**: Wrap retry policies (e.g., Polly, `EricksonLopez.Transaction.Resilience`) around the *outermost* `ExecuteAsync` invocation.
- **DON'T**: Catch database exceptions to retry individual SQL queries inside an active transaction.
- **Why**: In modern relational databases like PostgreSQL, any failed query puts the transaction in an aborted state (`SQLSTATE 25P02`). All subsequent queries within that transaction fail immediately.

```csharp
// ✅ CORRECT: Resilience policy retries the ENTIRE transaction boundary
await resiliencePipeline.ExecuteAsync(async ct =>
{
    await transactionManager.ExecuteAsync(async context =>
    {
        await repo1.UpdateAsync(context);
        await repo2.InsertAsync(context);
    }, cancellationToken: ct);
});
```

---

### Rule 6: Dual-Write Protection via Transactional Outbox
To publish domain events to message brokers safely:
- **DO**: Write both the domain aggregate state and the outbox message to the same relational database in the same transaction.
- **DO**: Use an asynchronous background worker (e.g. `BackgroundService`) to poll and publish outbox messages to Kafka/RabbitMQ.
- **DON'T**: Direct-publish to a message broker inside a database transaction (dual-write race condition).

---

### Rule 7: Guard Against Commit Ambiguity with Idempotency
- **DO**: Include an `idempotency_key` or unique business identifier in critical tables.
- **DO**: Catch `TransactionCommitException` when `ex.IsAmbiguous == true` and execute an idempotency check against the database before deciding whether to retry.

---

### Rule 8: Native AOT Compliance Invariants
- **DO**: Use static method groups, source-generated logging (`[LoggerMessage]`), and strongly typed configuration records.
- **DON'T**: Introduce unconstrained reflection, `MakeGenericType`, or dynamic IL generation (`System.Reflection.Emit`) in transaction lifecycles.

---

## 2. Anti-Patterns Catalog

| Anti-Pattern | Description | Remediation |
|---|---|---|
| **1. The "Chatty Repository" Transaction** | Repositories calling `BeginTransactionAsync()` independently for each entity update. | Move transaction coordination up to the Application Service layer. |
| **2. The "Silent Monad Commit"** | Using standard `ExecuteAsync` with functional `Result<T>` and returning failure without throwing. | Switch to `ExecuteResultAsync` from `EricksonLopez.Transaction.Result`. |
| **3. Distributed Two-Phase Commit (2PC)** | Attempting to coordinate transactions across multiple databases via MSDTC or network protocols. | Replace with Saga pattern, Eventual Consistency, and Transactional Outbox. |
| **4. The "HTTP Call in Transaction"** | Executing third-party API calls while holding open database row locks. | Perform HTTP calls outside the transaction or record an outbox command. |
| **5. Query Retry Inside Aborted Block** | Catching a SQL exception and re-running the query inside PostgreSQL. | Abort transaction and retry from the outer application boundary. |
| **6. The "Sensitive Identifier in Span" Leak** | Embedding sensitive transaction names (e.g., containing customer IDs or order references) in `TransactionOptions.TransactionName`, which then appears as-is in OTel spans. | Enable `TransactionOptions.SanitizeTelemetryMetadata = true` to replace `transaction.id` and `transaction.name` span tags with `[REDACTED]`. For SQL parameter redaction, configure OTel Processors at the application level. |
