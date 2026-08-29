# ADR-019: Lifecycle Exception Hook (`OnExceptionAsync`) in Transaction Enlistment

## Status
Accepted

## Context
Participants that enlist in transaction boundaries (such as Outbox dispatchers, audit logging adapters, idempotency stores, or external notification handlers) frequently need to clean up uncommitted buffers, notify observability pipelines, or record diagnostic telemetry when an unexpected failure occurs during transaction execution or commit.

Previously, `ITransactionEnlistment` supported `BeforeCommitAsync`, `AfterCommitAsync`, and `AfterRollbackAsync`, but lacked a direct notification callback receiving the specific `Exception` instance that caused the failure.

## Decision
1. Add `OnExceptionAsync` to `ITransactionEnlistment` with a non-breaking C# default interface implementation:
   ```csharp
   Task OnExceptionAsync(ITransactionContext context, Exception exception, CancellationToken cancellationToken = default) => Task.CompletedTask;
   ```
2. When an exception occurs during `CommitAsync` or `RollbackAsync`, `PhysicalTransaction` executes `ExecuteOnExceptionHooksAsync(ex, CancellationToken.None)`.
3. All exceptions thrown by individual `ITransactionEnlistment.OnExceptionAsync` implementations are silently caught and suppressed to prevent secondary failures from masking the primary root cause exception.

## Consequences

### Positive
- Enlisted components receive the precise exception causing transaction abort, allowing structured error reporting and outbox buffer disposal.
- 100% backward-compatible with existing `ITransactionEnlistment` implementations via default interface method.
- Secondary exception suppression guarantees deterministic error propagation to application callers.

### Negative
- Enlisted participants must ensure their `OnExceptionAsync` logic is fast and does not perform unmanaged blocking I/O.
