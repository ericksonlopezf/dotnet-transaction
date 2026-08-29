# ADR-013: Coexistence Model between Transaction and Unit of Work

## Status
Accepted

## Context
In Domain-Driven Design (DDD) and Clean Architecture, two concepts often become conflated:
1. **Unit of Work (UoW)**: A pattern from Martin Fowler for tracking aggregates modified during a business transaction and coordinating the writing out of changes and resolution of concurrency problems.
2. **Database Transaction (`ITransaction`)**: An infrastructure primitive provided by the database management system (RDBMS) ensuring ACID atomicity.

When frameworks blur this boundary, `IUnitOfWork` often duplicates transaction methods (`BeginTransaction`, `Commit`, `Rollback`, `Savepoints`), or `Transaction` attempts to act as an ORM change tracker.

## Decision
We define a strict **orthogonal coexistence model**:

```
Application Service / Use Case Handler
    │
    ├── Coordinates business logic & domain aggregates (Unit of Work)
    │
    └── Scopes atomic persistence boundary (ITransactionManager)
           │
           ├── Repository A.SaveAsync(aggregate, context)
           ├── Repository B.SaveAsync(aggregate, context)
           └── Outbox.StoreAsync(event, context)
```

1. **Transaction controls the transactional boundary (`DbTransaction` lifecycle, savepoints, isolation level, commit, rollback).**
2. **Unit of Work manages domain aggregate state transitions and business invariant verification.**
3. Repositories accept `ITransactionContext` to execute Dapper / raw SQL statements against the shared connection and transaction.

## Consequences
### Positive
- Zero duplication of transaction management logic.
- Clean DDD aggregate boundaries with high-performance Dapper data access.
- Repositories remain decoupled from transaction lifecycle control.

### Negative
- Developers must clearly differentiate business state tracking from database transaction lifecycles.
