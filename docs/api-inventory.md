# EricksonLopez.Transaction — Comprehensive Public API Inventory

> **Author:** Erickson Lopez ([ericksonlopezf@gmail.com](mailto:ericksonlopezf@gmail.com))  
> **Repository:** [github.com/ericksonlopezf/dotnet-transaction](https://github.com/ericksonlopezf/dotnet-transaction)  
> **Standard:** Complete Public API Surface of Core and Infrastructure Packages (52 Types)

---

## Overview

This inventory represents the **sole authoritative catalog** of all public types, interfaces, options, exceptions, test doubles, and extension methods exported by the `EricksonLopez.Transaction` library ecosystem. Only types originating from projects classified as **Core Library** or **Infrastructure** are included.

---

## 1. `EricksonLopez.Transaction.Abstractions` (Core Library)

**Namespace:** `EricksonLopez.Transaction`  
**Assembly:** `EricksonLopez.Transaction.Abstractions.dll`  
**Dependencies:** None (Pure BCL)

### Core Interfaces
| Type | Kind | Description | Key Members |
|---|---|---|---|
| `ITransactionManager` | `interface` | Primary coordinator for creating, executing, and orchestrating database transaction boundaries. | `CurrentContext`, `BeginAsync`, `ExecuteAsync` (4 overloads) |
| `ITransaction` | `interface : IAsyncDisposable` | Explicit handle to an active database transaction lifecycle. | `TransactionId`, `Context`, `State`, `CommitAsync`, `RollbackAsync`, `CreateSavepointAsync` |
| `ITransactionContext` | `interface : IAsyncDisposable` | Execution context containing active `DbConnection`, `DbTransaction`, state, cancellation tokens, and enlistment hooks. | `TransactionId`, `Connection`, `Transaction`, `State`, `IsolationLevel`, `CancellationToken`, `Enlistments`, `IsRollbackOnly`, `SetRollbackOnly`, `CreateSavepointAsync`, `Enlist` |
| `ISavepoint` | `interface : IAsyncDisposable` | Named transactional savepoint allowing partial rollback without aborting the outer transaction. | `Name`, `RollbackAsync`, `ReleaseAsync` |
| `ITransactionEnlistment` | `interface` | Contract for lifecycle participation in transaction commit, rollback, and exception stages. | `BeforeCommitAsync`, `AfterCommitAsync`, `AfterRollbackAsync`, `OnExceptionAsync` |
| `IDbConnectionFactory` | `interface` | Contract for creating open or unopened database connections. | `CreateConnectionAsync`, `CreateConnection` |
| `IDatabaseDialect` | `interface` | Contract abstracting provider-specific SQL syntax for savepoints and read-only transactions. | `CanHandle`, `ApplyReadOnlyModeAsync`, `GetSavepointCreationSql`, `GetSavepointRollbackSql`, `GetSavepointReleaseSql` |

### Configuration Records & Enums
| Type | Kind | Description | Values / Properties |
|---|---|---|---|
| `TransactionOptions` | `sealed record` | Immutable configuration record for transaction execution. | `IsolationLevel`, `Timeout`, `ReadOnly`, `NestedBehavior`, `TransactionName`, `SanitizeTelemetryMetadata`, `Default`, `Serializable`, `ReadOnlyMode`, `WithTimeout(TimeSpan)` |
| `TransactionIsolationLevel` | `enum` | Locking and isolation levels for transactions. | `Unspecified (0)`, `ReadUncommitted (1)`, `ReadCommitted (2)`, `RepeatableRead (3)`, `Serializable (4)`, `Snapshot (5)` |
| `NestedTransactionBehavior` | `enum` | Strategy when executing inside an already active ambient transaction. | `UseSavepoint (0)`, `RequireNew (1)`, `Suppress (2)`, `JoinExisting (3)` |
| `TransactionState` | `enum` | Deterministic transaction lifecycle states. | `Created (0)`, `Active (1)`, `Committed (2)`, `RolledBack (3)`, `Failed (4)`, `Disposed (5)` |

### Exceptions
**Namespace:** `EricksonLopez.Transaction.Exceptions`

| Type | Base Type | Description | Key Properties |
|---|---|---|---|
| `TransactionException` | `Exception` | Base exception for all transaction coordinator faults. | Standard exception properties |
| `TransactionCommitException` | `TransactionException` | Thrown when commit fails or commit outcome is uncertain due to timeout/network error. | `IsAmbiguous: bool` |
| `TransactionPostCommitException` | `TransactionException` | Thrown when physical database commit succeeded durably, but one or more post-commit hooks threw. | `InnerException` |
| `TransactionRollbackException` | `TransactionException` | Thrown when an explicit rollback fails during teardown. | `InnerException` |
| `TransactionStateException` | `TransactionException` | Thrown when an invalid lifecycle state transition is attempted. | `ActualState: TransactionState`, `AttemptedOperation: string?` |
| `TransactionTimeoutException` | `TransactionException` | Thrown when execution exceeds configured `TransactionOptions.Timeout`. | `Timeout: TimeSpan` |

---

## 2. `EricksonLopez.Transaction` (Core Library)

**Assembly:** `EricksonLopez.Transaction.dll`  
**Dependencies:** `EricksonLopez.Transaction.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Logging.Abstractions`, `OpenTelemetry.Api`

| Type | Namespace | Kind | Description | Key Members |
|---|---|---|---|---|
| `TransactionManager` | `EricksonLopez.Transaction` | `sealed partial class : ITransactionManager` | Production transaction coordinator using `AsyncLocal` context propagation and connection pooling. | Constructor `(IDbConnectionFactory, ILogger?, IEnumerable<IDatabaseDialect>?)`, `CurrentContext`, `BeginAsync`, `ExecuteAsync` |
| `DelegateDbConnectionFactory` | `EricksonLopez.Transaction` | `sealed class : IDbConnectionFactory` | Lightweight connection factory wrapping sync or async connection delegates. | Constructors: `(Func<DbConnection>)`, `(Func<CancellationToken, ValueTask<DbConnection>>)` |
| `TransactionDiagnostics` | `EricksonLopez.Transaction.Diagnostics` | `static class` | OpenTelemetry distributed tracing and metrics provider (`"EricksonLopez.Transaction"`). | `SourceName`, `Version`, `ActivitySource`, `Meter`, `StartActivity`, `RecordStarted`, `RecordCommitted`, `RecordRolledBack`, `RecordFailed`, `RecordSavepointCreated`, `RecordSavepointRolledBack`, `RecordSavepointReleased` |
| `TransactionServiceCollectionExtensions` | `Microsoft.Extensions.DependencyInjection` | `static class` | DI extension methods registering `ITransactionManager` and `IDbConnectionFactory`. | `AddTransaction<TFactory>()`, `AddTransaction(Func<IServiceProvider, IDbConnectionFactory>)`, `AddTransaction(Func<IServiceProvider, CancellationToken, ValueTask<DbConnection>>)`, `AddTransaction(Func<IServiceProvider, DbConnection>)` |

---

## 3. `EricksonLopez.Transaction.Dapper` (Infrastructure)

**Namespace:** `EricksonLopez.Transaction.Dapper`  
**Assembly:** `EricksonLopez.Transaction.Dapper.dll`  
**Dependencies:** `EricksonLopez.Transaction.Abstractions`, `Dapper`

| Type | Kind | Description | Key Members |
|---|---|---|---|
| `TransactionDapperExtensions` | `static class` | High-performance Dapper extension methods on `ITransactionContext`. Automatically binds connection, transaction, and cancellation token. | `AsCommand`, `ExecuteAsync`, `QueryAsync<T>`, `QuerySingleOrDefaultAsync<T>`, `QueryFirstOrDefaultAsync<T>`, `QuerySingleAsync<T>`, `QueryFirstAsync<T>`, `ExecuteScalarAsync<T>`, `QueryMultipleAsync`, `ExecuteReaderAsync` |

---

## 4. `EricksonLopez.Transaction.EntityFrameworkCore` (Infrastructure)

**Namespace:** `EricksonLopez.Transaction.EntityFrameworkCore`  
**Assembly:** `EricksonLopez.Transaction.EntityFrameworkCore.dll`  
**Dependencies:** `EricksonLopez.Transaction.Abstractions`, `Microsoft.EntityFrameworkCore`

| Type | Kind | Description | Key Members |
|---|---|---|---|
| `DbContextTransactionExtensions` | `static class` | Extension methods enabling EF Core `DbContext` to join active transaction boundaries. | `UseTransactionAsync(this DbContext, ITransactionContext, CancellationToken)` |

---

## 5. `EricksonLopez.Transaction.Mediator` (Infrastructure)

**Namespace:** `EricksonLopez.Transaction.Mediator`  
**Assembly:** `EricksonLopez.Transaction.Mediator.dll`  
**Dependencies:** `EricksonLopez.Transaction.Abstractions`, `EricksonLopez.Mediator`, `EricksonLopez.Result`

| Type | Kind | Description | Key Members |
|---|---|---|---|
| `ITransactionalCommand` | `interface` | Marker interface designating a mediator request as requiring an automatic database transaction. | Marker interface |
| `ITransactionalCommandOptions` | `interface` | Interface allowing transactional commands to supply custom options without reflection (AOT-safe). | `TransactionOptions TransactionOptions { get; }` |
| `TransactionPipelineBehavior<TRequest, TResponse>` | `sealed class : IPipelineBehavior<TRequest, TResponse>` | Open-generic pipeline behavior wrapping mediator command dispatch in an automatic transaction. | `Handle<TNext>(TRequest, TNext, CancellationToken)` |
| `TransactionMediatorServiceCollectionExtensions` | `static class` | DI registration extensions for mediator pipeline behavior. | `AddTransactionPipelineBehavior(this IServiceCollection)` |
| `TransactionalAttribute` | `sealed class : Attribute` | Declarative metadata attribute for specifying transaction isolation level and timeout on mediator commands. Use for reflection-based scenarios. For Native AOT, implement `ITransactionalCommandOptions` instead (no reflection required). | `IsolationLevel`, `TimeoutSeconds` |

---

## 6. `EricksonLopez.Transaction.Resilience` (Infrastructure)

**Namespace:** `EricksonLopez.Transaction.Resilience`  
**Assembly:** `EricksonLopez.Transaction.Resilience.dll`  
**Dependencies:** `EricksonLopez.Transaction.Abstractions`, `Polly`

| Type | Kind | Description | Key Members |
|---|---|---|---|
| `PollyTransactionExtensions` | `static class` | Polly policy builder extensions for handling commit ambiguity. | `HandleAmbiguousCommit(this PolicyBuilder)`, `HandleAmbiguousCommit()` |

---

## 7. `EricksonLopez.Transaction.Result` (Infrastructure)

**Namespace:** `EricksonLopez.Transaction.Result`  
**Assembly:** `EricksonLopez.Transaction.Result.dll`  
**Dependencies:** `EricksonLopez.Transaction.Abstractions`, `EricksonLopez.Result`

| Type | Kind | Description | Key Members |
|---|---|---|---|
| `TransactionResultExtensions` | `static class` | Monadic transaction extensions on `ITransactionManager` triggering automatic rollback on `Result.IsFailure`. | `ExecuteResultAsync(Func<ITransactionContext, Task<Result>>) `, `ExecuteResultAsync(Func<Task<Result>>) `, `ExecuteResultAsync<TValue>(Func<ITransactionContext, Task<Result<TValue>>>) `, `ExecuteResultAsync<TValue>(Func<Task<Result<TValue>>>) ` |

---

## 8. `EricksonLopez.Transaction.Testing` (Infrastructure)

**Namespace:** `EricksonLopez.Transaction.Testing`  
**Assembly:** `EricksonLopez.Transaction.Testing.dll`  
**Dependencies:** `EricksonLopez.Transaction.Abstractions`

| Type | Kind | Description | Key Members |
|---|---|---|---|
| `FakeTransactionManager` | `sealed class : ITransactionManager` | In-memory transaction manager for unit testing without database instances. | `StartedTransactions`, `ExceptionToThrowOnCommit`, `CurrentContext`, `BeginAsync`, `ExecuteAsync` (4 overloads) |
| `FakeTransaction` | `sealed class : ITransaction` | In-memory transaction double recording lifecycle invocations. | `TransactionId`, `Context`, `State`, `CommitCount`, `RollbackCount`, `ExceptionToThrowOnCommit`, `ExceptionToThrowOnRollback`, `IsDisposed`, `CommitAsync`, `RollbackAsync`, `CreateSavepointAsync` |
| `FakeTransactionContext` | `sealed class : ITransactionContext` | In-memory transaction execution context recording savepoint creation and enlistments. | `TransactionId`, `State`, `IsolationLevel`, `CancellationToken`, `Enlistments`, `CreatedSavepoints`, `IsRollbackOnly`, `SetRollbackOnly`, `CreateSavepointAsync`, `Enlist` |

---

## 9. `EricksonLopez.Transaction.Analyzers` (Infrastructure / Code Analysis)

**Namespace:** `EricksonLopez.Transaction.Analyzers`  
**Assembly:** `EricksonLopez.Transaction.Analyzers.dll`  
**Dependencies:** `Microsoft.CodeAnalysis.CSharp`

| Type | Kind | Description | Key Members |
|---|---|---|---|
| `ConnectionManipulationAnalyzer` | `class : DiagnosticAnalyzer` | Roslyn diagnostic analyzer enforcing rule `ELT001` against manual connection lifecycle manipulation on `ITransactionContext`. | `DiagnosticId = "ELT001"`, `SupportedDiagnostics`, `Initialize` |

---

## 10. Relational Dialect Provider Packages (Infrastructure)

Each dialect package provides:
1. An `IDbConnectionFactory` implementation.
2. A static error classifier identifying transient faults, deadlocks, and serialization conflicts.
3. DI registration extension methods in `Microsoft.Extensions.DependencyInjection`.

### PostgreSQL (`EricksonLopez.Transaction.PostgreSql`)
- `PostgreSqlConnectionFactory`: `IDbConnectionFactory` backed by `NpgsqlDataSource`.
- `PostgreSqlErrorClassifier`: Classifies SQLSTATE `40001` (Serialization), `40P01` (Deadlock), `25P02` (Aborted transaction), `55P03` (Lock timeout).
- `PostgreSqlTransactionExtensions`: `AddPostgreSqlTransaction(connectionString)`, `AddPostgreSqlTransaction(NpgsqlDataSource)`.

### Microsoft SQL Server (`EricksonLopez.Transaction.SqlServer`)
- `SqlServerConnectionFactory`: `IDbConnectionFactory` using `Microsoft.Data.SqlClient.SqlConnection`.
- `SqlServerErrorClassifier`: Classifies Error 1205 (Deadlock), 3960/3961 (Snapshot Conflict), 1222 (Lock timeout).
- `SqlServerTransactionExtensions`: `AddSqlServerTransaction(connectionString)`.

### MySQL (`EricksonLopez.Transaction.MySql`)
- `MySqlConnectionFactory`: `IDbConnectionFactory` using `MySqlConnector.MySqlConnection`.
- `MySqlErrorClassifier`: Classifies Error 1213 (Deadlock), 1205 (Lock wait timeout).
- `MySqlTransactionExtensions`: `AddMySqlTransaction(connectionString)`.

### MariaDB (`EricksonLopez.Transaction.MariaDb`)
- `MariaDbConnectionFactory`: `IDbConnectionFactory` using `MySqlConnector.MySqlConnection`.
- `MariaDbErrorClassifier`: Classifies Error 1213 (Deadlock), 1205 (Lock wait timeout).
- `MariaDbTransactionExtensions`: `AddMariaDbTransaction(connectionString)`.

### Oracle Database (`EricksonLopez.Transaction.Oracle`)
- `OracleConnectionFactory`: `IDbConnectionFactory` using `Oracle.ManagedDataAccess.Client.OracleConnection`.
- `OracleErrorClassifier`: Classifies `ORA-00060` (Deadlock), `ORA-08177` (Serialization failure), `ORA-30006` (Lock timeout).
- `OracleTransactionExtensions`: `AddOracleTransaction(connectionString)`.

### SQLite (`EricksonLopez.Transaction.Sqlite`)
- `SqliteConnectionFactory`: `IDbConnectionFactory` using `Microsoft.Data.Sqlite.SqliteConnection`.
- `SqliteErrorClassifier`: Classifies `SQLITE_BUSY (5)`, `SQLITE_LOCKED (6)`.
- `SqliteTransactionExtensions`: `AddSqliteTransaction(connectionString)`.
