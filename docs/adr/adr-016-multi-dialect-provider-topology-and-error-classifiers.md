# ADR-016: Multi-Dialect Provider Topology and Error Classifiers

## Status
Accepted

## Context
The EricksonLopez ecosystem supports multiple relational database engines across enterprise workloads:
1. PostgreSQL (`Npgsql`)
2. SQL Server (`Microsoft.Data.SqlClient`)
3. MySQL (`MySqlConnector`)
4. MariaDB (`MySqlConnector`)
5. Oracle (`Oracle.ManagedDataAccess.Core`)
6. SQLite (`Microsoft.Data.Sqlite`)

Each database engine features distinct connection lifecycles, error numbers, SQLSTATE codes, deadlock detection tokens, and concurrency conflict semantics (e.g. SQLite database locks vs SQL Server snapshot conflicts vs PostgreSQL 40001 serialization conflicts).

## Decision
We establish dedicated dialect adapter packages for each supported database engine:
- `EricksonLopez.Transaction.PostgreSql`
- `EricksonLopez.Transaction.SqlServer`
- `EricksonLopez.Transaction.MySql`
- `EricksonLopez.Transaction.MariaDb`
- `EricksonLopez.Transaction.Oracle`
- `EricksonLopez.Transaction.Sqlite`

Each dialect package provides:
1. An engine-specific `IDbConnectionFactory` managing proper connection strings and pooled connection lifecycle.
2. A high-performance static `ErrorClassifier` with zero-allocation exception inspection (`IsDeadlock`, `IsSerializationFailure`/`IsBusyOrLocked`, `IsTransient`).
3. Clean DI extension methods (`Add<Dialect>Transaction`).

## Consequences
- **Positive**: Applications only reference their target database driver without taking transitive dependencies on unused database SDKs.
- **Positive**: Zero reflection overhead, 100% Native AOT trimming safe.
- **Positive**: Standardized resilience predicate contract across all database engines in the ecosystem.
