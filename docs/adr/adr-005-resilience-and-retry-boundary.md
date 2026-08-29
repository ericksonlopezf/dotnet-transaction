# ADR-005: Resilience Policy and Transaction Retry Boundary

## Status
Accepted

## Context
When executing database operations in high-concurrency environments, transient failures can occur:
- PostgreSQL Serialization Conflict (`SQLSTATE 40001` under `Serializable` or `RepeatableRead`).
- Deadlock Detected (`SQLSTATE 40P01`).
- Transient Network / Connection Drops.

A common anti-pattern is placing a retry policy (such as Polly or `EricksonLopez.Resilience`) around individual SQL queries *inside* an already active database transaction. In PostgreSQL, when an error occurs inside a transaction block, the transaction immediately enters the aborted state (`SQLSTATE 25P02: current transaction is aborted, commands ignored until end of transaction block`). Retrying an individual command within that aborted transaction fails repeatedly.

## Decision
1. `EricksonLopez.Transaction` **MUST NOT** include an internal retry loop.
2. Resilience and retry policies (e.g. Polly, `EricksonLopez.Resilience`) **MUST wrap the entire transaction block from the outside**, including the creation of the transaction boundary:
   ```csharp
   // CORRECT: Retry policy wraps the complete transaction boundary
   await resiliencePipeline.ExecuteAsync(async cancellationToken =>
   {
       await transactionManager.ExecuteAsync(async context =>
       {
           await repository.SaveOrderAsync(order, context.CancellationToken);
           await outbox.StoreAsync(orderCreatedEvent, context);
       }, options, cancellationToken);
   });
   ```
3. When a transient error occurs, the active transaction is rolled back and disposed. The outer retry policy opens a brand new connection and transaction with clean initial state.

## Consequences
### Positive
- Correct handling of PostgreSQL `25P02` (aborted transaction block) and `40001` (serialization failure).
- Clear architectural separation between transaction coordination (`EricksonLopez.Transaction`) and resilience policies (`EricksonLopez.Resilience`).
- Zero retry pollution or leaky retry abstractions inside transaction coordinators.

### Negative
- Developers must configure outer resilience pipelines explicitly.
