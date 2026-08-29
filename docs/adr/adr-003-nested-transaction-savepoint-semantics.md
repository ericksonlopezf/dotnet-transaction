# ADR-003: Nested Transaction Semantics via Database Savepoints, Participation, Isolation, and Suppression

## Status
Accepted

## Context
Relational database engines (such as PostgreSQL, MySQL, SQLite, and SQL Server) do not support true physical nested transactions on a single `DbConnection`. In standard ADO.NET, calling `BeginTransactionAsync()` on an already active connection throws an `InvalidOperationException`.

However, enterprise application architectures frequently compose layered application services where an outer use case invokes one or more inner services, each of which requests transactional boundary protection. Furthermore, specific scenarios require non-transactional auditing (suppression) or completely isolated transactions on independent connections.

## Decision
We define 4 deterministic nested transaction behaviors via `NestedTransactionBehavior`:

### 1. `NestedTransactionBehavior.UseSavepoint` (Default)
When `BeginAsync` or `ExecuteAsync` is called while an active ambient transaction exists:
- A uniquely named savepoint (`sp_{Guid}`) is created on the active physical transaction.
- A `SavepointTransactionScope` is returned that controls the savepoint lifecycle.
- If the nested scope completes and commits, the savepoint changes are retained in the outer transaction.
- If the nested scope fails or rolls back, changes executed since the savepoint are rolled back (`ROLLBACK TO SAVEPOINT`), allowing the outer transaction to remain active and make recovery decisions.

### 2. `NestedTransactionBehavior.JoinExisting`
- The nested scope participates directly in the active transaction without creating savepoints.
- A failure or rollback in the nested scope invalidates the parent transaction.

### 3. `NestedTransactionBehavior.RequireNew`
- Suspends the current ambient transaction and opens a new physical connection and independent database transaction.
- Commits or rollbacks in the new transaction do not impact the parent transaction.

### 4. `NestedTransactionBehavior.Suppress`
- Suspends the active ambient transaction (`AmbientContextHolder.Value = null`).
- Executes the operation non-transactionally without ambient context pollution.
- Automatically restores the previous ambient transaction context upon disposal of the scope.
- Throws an informative `InvalidOperationException` if an attempt is made to access `ITransaction.Context` or create savepoints on a suppressed scope.

## Consequences

### Positive
- Natural composition of transactional application services without driver runtime exceptions.
- Granular failure isolation within complex business workflows via SQL savepoints.
- Complete parity with modern transaction scope paradigms, enabling non-transactional auditing inside transactional scopes via `Suppress`.
- Consistent behavior across all supported dialects (PostgreSQL, SQL Server, MySQL, MariaDB, Oracle, SQLite).

### Negative
- Savepoints hold row locks acquired by statements until the outermost physical transaction commits or rolls back.
