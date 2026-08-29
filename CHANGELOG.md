# Changelog

All notable changes to `EricksonLopez.Transaction` will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

---

## [1.0.0] - 2026-08-29

### Added
- **Core Abstractions (`EricksonLopez.Transaction.Abstractions`)**:
  - `ITransactionManager` primary coordinator interface for explicit (`BeginAsync`) and automatic (`ExecuteAsync`) transaction lifecycle workflows.
  - Parameterless and context-receiving transactional execution overloads: `ExecuteAsync(Func<Task>)`, `ExecuteAsync(Func<ITransactionContext, Task>)`, `ExecuteAsync<TResult>(Func<Task<TResult>>)`. and `ExecuteAsync<TResult>(Func<ITransactionContext, Task<TResult>>)`.
  - `ITransaction` explicit transaction lifecycle handle with `CommitAsync`, `RollbackAsync`, and `CreateSavepointAsync` supporting `IAsyncDisposable` with automatic rollback on disposal if uncommitted.
  - `ITransactionContext` providing access to active `DbConnection`, `DbTransaction`, `TransactionState`, `TransactionIsolationLevel`, `CancellationToken`, and enlistment management.
  - `ISavepoint` contract enabling nested partial rollback and explicit release in engines supporting savepoint destruction.
  - `ITransactionEnlistment` participant lifecycle hooks (`BeforeCommitAsync`, `AfterCommitAsync`, `AfterRollbackAsync`, and `OnExceptionAsync`) with default implementations.
  - `IDbConnectionFactory` contract supporting both synchronous (`CreateConnection`) and asynchronous (`CreateConnectionAsync`) connection instantiation.
  - `TransactionOptions` immutable record supporting `IsolationLevel`, `Timeout`, `ReadOnly`, `NestedBehavior`, and `TransactionName` with static factory helpers (`Default`, `Serializable`, `ReadOnlyMode`, `WithTimeout`).
  - `TransactionIsolationLevel` enum spanning `ReadUncommitted`, `ReadCommitted`, `RepeatableRead`, `Serializable`, and `Snapshot`.
  - `TransactionState` deterministic state machine states (`Created`, `Active`, `Committed`, `RolledBack`, `Failed`, `Disposed`).
  - `NestedTransactionBehavior` enum specifying nested boundary semantics: `UseSavepoint` (default), `RequireNew`, `Suppress`, and `JoinExisting`.
  - Structured exception hierarchy under `EricksonLopez.Transaction.Exceptions`: `TransactionException`, `TransactionCommitException` (with `IsAmbiguous` classification), `TransactionRollbackException`, `TransactionStateException`, and `TransactionTimeoutException`.
- **Core Engine & Implementation (`EricksonLopez.Transaction`)**:
  - `TransactionManager` coordinating ambient `AsyncLocal` context flows, timeout propagation, and execution modes.
  - `TransactionStateMachine` enforcing strict, thread-safe, deterministic lifecycle state transitions.
  - `PhysicalTransaction` managing physical ADO.NET connection and transaction lifecycles, structured logging, and metrics recording.
  - Nested transaction scope adapters:
    - `SavepointTransactionScope` mapping nested boundaries into relational savepoints with partial rollback isolation.
    - `JoinExistingTransactionScope` providing all-or-nothing participation in outer transactional scopes.
    - `SuppressedTransactionScope` executing operations non-transactionally with automatic ambient context suspension and restoration upon disposal.
  - Physical read-only mode propagation (`SET TRANSACTION READ ONLY;` for PostgreSQL and MySQL session drivers) when `TransactionOptions.ReadOnly` is enabled.
  - High-performance zero-allocation structured diagnostic logging via `[LoggerMessage]`.
  - OpenTelemetry distributed tracing (`ActivitySource`) and metric instruments (`Meter`) registered under `"EricksonLopez.Transaction"`.
  - Microsoft Dependency Injection extensions (`services.AddTransaction<TFactory>()` and delegate-based registrations).
  - `DelegateDbConnectionFactory` wrapping caller-supplied synchronous and asynchronous connection factory delegates.
- **Dapper Integration (`EricksonLopez.Transaction.Dapper`)**:
  - `context.AsCommand(...)` building immutable, properly bound Dapper `CommandDefinition` structs with merged cancellation tokens.
  - High-performance asynchronous query and execution extensions on `ITransactionContext`: `ExecuteAsync`, `QueryAsync<T>`, `QuerySingleOrDefaultAsync<T>`, `QueryFirstOrDefaultAsync<T>`, `QuerySingleAsync<T>`, `QueryFirstAsync<T>`, and `ExecuteScalarAsync<T>`.
  - Multi-result set and raw reader extensions: `QueryMultipleAsync` returning `SqlMapper.GridReader` and `ExecuteReaderAsync` returning `IDataReader`.
- **Result Pattern Integration (`EricksonLopez.Transaction.Result`)**:
  - Integration with `EricksonLopez.Result` monad.
  - `ExecuteResultAsync` and `ExecuteResultAsync<TValue>` overloads for both parameterless and context-receiving operations with automatic commit on `Result.Success` and automatic rollback on `Result.Failure` without exception overhead.
- **Relational Database Provider Adapters**:
  - **PostgreSQL (`EricksonLopez.Transaction.PostgreSql`)**:
    - `PostgreSqlConnectionFactory` backed by `NpgsqlDataSource`.
    - `PostgreSqlErrorClassifier` diagnosing SQLSTATE error codes `40001` (serialization failure), `40P01` (deadlock), `25P02` (in-failed-transaction), `57014` (query canceled), `55P03` (lock timeout), and connection failures.
    - Dependency injection extensions: `services.AddPostgreSqlTransaction(...)`.
  - **Microsoft SQL Server (`EricksonLopez.Transaction.SqlServer`)**:
    - `SqlServerConnectionFactory` backed by `Microsoft.Data.SqlClient`.
    - `SqlServerErrorClassifier` diagnosing Error Numbers 1205 (deadlock), 3960/3961 (snapshot conflicts), 1222 (lock request timeout), and network errors.
    - Dependency injection extensions: `services.AddSqlServerTransaction(...)`.
  - **MySQL (`EricksonLopez.Transaction.MySql`)**:
    - `MySqlConnectionFactory` backed by `MySqlConnector`.
    - `MySqlErrorClassifier` diagnosing Error Numbers 1213 (deadlock), 1205 (lock wait timeout), and transient connection dropouts.
    - Dependency injection extensions: `services.AddMySqlTransaction(...)`.
  - **MariaDB (`EricksonLopez.Transaction.MariaDb`)**:
    - `MariaDbConnectionFactory` backed by `MySqlConnector`.
    - `MariaDbErrorClassifier` diagnosing MariaDB concurrency conflicts and deadlock conditions.
    - Dependency injection extensions: `services.AddMariaDbTransaction(...)`.
  - **Oracle Database (`EricksonLopez.Transaction.Oracle`)**:
    - `OracleConnectionFactory` backed by `Oracle.ManagedDataAccess.Core`.
    - `OracleErrorClassifier` diagnosing ORA-00060 (deadlock), ORA-08177 (serialization failure), ORA-30006 (lock timeout), and connection failures.
    - Dependency injection extensions: `services.AddOracleTransaction(...)`.
  - **SQLite (`EricksonLopez.Transaction.Sqlite`)**:
    - `SqliteConnectionFactory` backed by `Microsoft.Data.Sqlite`.
    - `SqliteErrorClassifier` diagnosing `SQLITE_BUSY` (5) and `SQLITE_LOCKED` (6) contention.
    - Dependency injection extensions: `services.AddSqliteTransaction(...)`.
- **Testing Doubles (`EricksonLopez.Transaction.Testing`)**:
  - `FakeTransactionManager`, `FakeTransactionContext`, and `FakeTransaction` for unit and integration testing without database dependencies.
  - Failure injection and assertion inspection via `StartedTransactions`, `CreatedSavepoints`, `CommitCount`, `RollbackCount`, `ExceptionToThrowOnCommit`, and `ExceptionToThrowOnRollback`.
- **Cross-Cutting Quality & Compliance Architecture**:
  - Multi-targeting for `.NET 8.0`, `.NET 9.0`, and `.NET 10.0` across all solution packages.
  - Native AOT trimming compatibility enforced with `<IsAotCompatible>true</IsAotCompatible>` and verified by `EricksonLopez.Transaction.AotSmokeTest`.
  - Strong-name signing across all assemblies (`SignAssembly=true`) using `EricksonLopez.snk`.
  - Architecture Decision Records (`ADR-001` through `ADR-026`) documenting transactional semantics, error classification, telemetry invariants, and architectural guardrails.

---

[Unreleased]: https://github.com/ericksonlopezf/dotnet-transaction/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/ericksonlopezf/dotnet-transaction/releases/tag/v1.0.0
