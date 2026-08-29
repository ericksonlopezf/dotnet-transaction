# ADR-002: Package Segregation and Dependency Topology

## Status
Accepted

## Context
A monolithic transaction library introduces unwanted dependencies (such as Dapper, Npgsql, or Result types) into core application layers that may only require pure transaction contracts. Conversely, splitting packages too finely introduces maintenance friction and version skew.

## Decision
We structure `EricksonLopez.Transaction` into 6 cohesive packages:
1. `EricksonLopez.Transaction.Abstractions`: Pure BCL contracts (`ITransaction`, `ITransactionContext`, `ITransactionManager`, `ISavepoint`, `TransactionOptions`, `TransactionState`). Zero third-party dependencies.
2. `EricksonLopez.Transaction`: Core transaction coordinator, state machine, ambient context manager, and OpenTelemetry instrumentation.
3. `EricksonLopez.Transaction.Dapper`: Dapper-specific extension methods and `CommandDefinition` enrichment.
4. `EricksonLopez.Transaction.PostgreSql`: PostgreSQL provider adapters, `NpgsqlDataSource` factories, and SQLSTATE error classification.
5. `EricksonLopez.Transaction.Result`: Functional integration with `EricksonLopez.Result` for automatic commit on success and rollback on failure.
6. `EricksonLopez.Transaction.Testing`: In-memory test doubles (`FakeTransactionManager`, `FakeTransactionContext`, `SpyTransaction`).

## Consequences
### Positive
- Application layers can depend strictly on `EricksonLopez.Transaction.Abstractions`.
- Zero unnecessary package bloat or transitive provider dependencies in domain/application projects.
- Full Native AOT trimming compatibility across all packages.

### Negative
- Consumers choose and install the specific integration packages required for their stack (e.g. Dapper, PostgreSQL, Result).
