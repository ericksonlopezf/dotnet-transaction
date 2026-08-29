# ADR-001: Explicit Transaction Boundary Ownership

## Status
Accepted

## Context
In enterprise .NET Clean Architecture applications using Domain-Driven Design (DDD) and Dapper with PostgreSQL, persistence operations often span multiple aggregate roots, outbox message tables, and idempotency stores within a single business use case. 

When transaction boundaries are ad-hoc or implicitly managed:
1. Individual repositories often instantiate independent `DbConnection` instances, preventing atomic commit across operations.
2. Repositories attempt to invoke `Commit()` or `Rollback()`, violating Single Responsibility and persistence ignorance.
3. Errors during intermediate steps leave partial writes committed in the database.

## Decision
We enforce that **the transaction boundary is owned explicitly by the Application Service / Use Case layer** (or incoming pipeline middleware), orchestrated through `ITransactionManager` or `ITransactionContext`.

1. **Repositories MUST NOT create, commit, or rollback transactions.**
2. Repositories participate in the active transaction by receiving `ITransactionContext` or accessing the ambient context coordinated by `ITransactionManager`.
3. The `ITransaction` lifecycle is scoped using `await using` blocks, ensuring automatic rollback on unhandled exceptions or disposal prior to explicit commit.

## Consequences
### Positive
- Strict atomic boundary across multiple repositories, outbox records, and idempotency claims.
- Repositories remain clean, focused solely on data mapping and SQL execution.
- Guaranteed rollback upon unhandled exception or cancellation.

### Negative
- Application services must be aware of transaction coordination interfaces (`ITransactionManager`).
