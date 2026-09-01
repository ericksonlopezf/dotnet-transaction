# ADR-007: Transaction Context Propagation via MediatR Pipeline

## Status
Accepted

## Context
In CQRS architectures using `MediatR`, commands often require a database transaction to ensure atomicity across multiple repository operations or when writing integration events to an Outbox. Passing `IDbTransaction` manually through handlers breaks the Clean Architecture boundaries, as Application logic should not depend on `System.Data` directly. Furthermore, in PostgreSQL, if Row-Level Security (RLS) is used, the transaction context must also set the `TenantId` via `set_config` before any queries run.

## Decision
1. **MediatR Transaction Pipeline Behavior**: Introduce `TransactionBehavior<TRequest, TResponse>` inside the `EricksonLopez.Transaction.MediatR` package.
2. **Unit of Work Abstraction**: Handlers rely on an `IUnitOfWork` (or just pure repositories) while the `TransactionBehavior` transparently initiates an `IDbTransaction`.
3. **Execution Flow**:
   - The behavior checks if the request is annotated with an `[Transactional]` attribute (or implements an `ITransactionalCommand` interface).
   - If transactional, it resolves the `IUnitOfWork` (or a `DbConnection` factory).
   - It begins the transaction.
   - It invokes `next()`.
   - If `next()` returns a `Result.Success`, it calls `.Commit()`. If it throws or returns `Result.Failure`, it calls `.Rollback()`.
4. **Integration with MultiTenancy**: The behavior will provide an extension point (`ITransactionInitializer`) that `EricksonLopez.MultiTenancy` can hook into to automatically call `set_config('app.current_tenant_id', ...)` immediately after the transaction starts.

## Consequences
- **Positive**: Complete separation of Application logic from persistence transaction mechanics. Handlers are pure. Eliminates boilerplate `try/catch/rollback` across the codebase.
- **Negative**: Hidden control flow. Database locks might be held slightly longer (around the entire handler execution, including potential external API calls, which must be strictly avoided by moving external calls out of the transactional handler).
