# ADR-018: ReadOnly Transaction Mode Propagation to Database Engines

## Status
Accepted

## Context
Modern cloud-native databases (including PostgreSQL, MySQL, MariaDB, and read-replica routing clusters) provide specialized performance optimizations, lower lock contention, and strict write-prevention invariants for read-only transactions. In ADO.NET, setting `IsolationLevel` does not communicate read-only intent to the database engine.

When developers configure `TransactionOptions.ReadOnlyMode` or set `options.ReadOnly = true`, the transaction coordinator must enforce this intent at the physical database session level where supported by the relational engine.

## Decision
We enforce read-only transaction semantics across database engines as follows:

1. When `TransactionOptions.ReadOnly == true`, `TransactionManager` executes dialect-specific read-only configuration immediately after opening the physical database transaction and before executing any user queries.
2. **PostgreSQL**: Executes `SET TRANSACTION READ ONLY;` directly within the active transaction block. Any subsequent `INSERT`, `UPDATE`, or `DELETE` attempt causes PostgreSQL to reject the query with SQLSTATE `25006` (`read_only_sql_transaction`).
3. **MySQL / MariaDB**: Executes `SET TRANSACTION READ ONLY;` where supported by server version and session capability.
4. If a dialect does not provide an explicit transaction-level read-only SQL command, the transaction proceeds without failing, but the read-only flag remains tracked in metadata and OpenTelemetry instrumentation tags.

## Consequences

### Positive
- Strict database-enforced immutability guarantees for read-only queries and read-side CQRS operations.
- Compatible with database read-replica routing proxies (such as PgBouncer or AWS Aurora read-endpoints) that monitor read-only transaction state.
- Zero extra overhead when `ReadOnly = false` (default).

### Negative
- Adds one lightweight round-trip query (`SET TRANSACTION READ ONLY;`) upon beginning a physical read-only transaction on PostgreSQL.
