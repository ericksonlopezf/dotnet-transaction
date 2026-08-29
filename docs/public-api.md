# EricksonLopez.Transaction — Public API Reference

> **Copyright © Erickson Lopez. MIT License.**
> **Author:** Erickson Lopez ([ericksonlopezf@gmail.com](mailto:ericksonlopezf@gmail.com))
> **Repository:** [github.com/ericksonlopezf/dotnet-transaction](https://github.com/ericksonlopezf/dotnet-transaction)

---

## Overview

This document is the authoritative public API reference for all packages in the `EricksonLopez.Transaction` ecosystem, derived directly from source code and XML documentation comments. All types target `net8.0;net9.0;net10.0` unless noted otherwise.

---

## Package: `EricksonLopez.Transaction.Abstractions`

Pure BCL contracts with zero external dependencies. All other packages in the ecosystem depend on this package.

**Namespace**: `EricksonLopez.Transaction`

---

### `ITransactionManager` (interface)

Primary coordinator for creating, executing, and orchestrating database transaction boundaries.

```csharp
public interface ITransactionManager
```

| Member | Signature | Description |
|---|---|---|
| `CurrentContext` | `ITransactionContext? CurrentContext { get; }` | Gets the ambient transaction context on the current asynchronous flow, or `null` if no transaction is active. |
| `BeginAsync` | `Task<ITransaction> BeginAsync(TransactionOptions? options = null, CancellationToken cancellationToken = default)` | Begins a new transaction explicitly with the specified options. |
| `ExecuteAsync` | `Task ExecuteAsync(Func<ITransactionContext, Task> operation, TransactionOptions? options = null, CancellationToken cancellationToken = default)` | Executes a delegate within an automatic transaction boundary (context-receiving overload). |
| `ExecuteAsync` | `Task ExecuteAsync(Func<Task> operation, TransactionOptions? options = null, CancellationToken cancellationToken = default)` | Executes a parameterless delegate within an automatic transaction boundary. |
| `ExecuteAsync<TResult>` | `Task<TResult> ExecuteAsync<TResult>(Func<ITransactionContext, Task<TResult>> operation, TransactionOptions? options = null, CancellationToken cancellationToken = default)` | Executes a context-receiving delegate and returns the result. |
| `ExecuteAsync<TResult>` | `Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation, TransactionOptions? options = null, CancellationToken cancellationToken = default)` | Executes a parameterless delegate and returns the result. |

---

### `ITransaction` (interface)

Explicit handle to an active database transaction lifecycle. Must be consumed in an `await using` block.

```csharp
public interface ITransaction : IAsyncDisposable
```

> **Auto-Rollback**: If `CommitAsync` is not called before disposal, the transaction is automatically rolled back.

| Member | Signature | Description |
|---|---|---|
| `TransactionId` | `Guid TransactionId { get; }` | Unique identifier of the transaction. |
| `Context` | `ITransactionContext Context { get; }` | Execution context associated with this transaction. |
| `State` | `TransactionState State { get; }` | Current lifecycle state of the transaction. |
| `CommitAsync` | `Task CommitAsync(CancellationToken cancellationToken = default)` | Commits the active transaction and persists all changes atomically. |
| `RollbackAsync` | `Task RollbackAsync(CancellationToken cancellationToken = default)` | Rolls back the active transaction and discards all uncommitted modifications. |
| `CreateSavepointAsync` | `Task<ISavepoint> CreateSavepointAsync(string name, CancellationToken cancellationToken = default)` | Creates a named savepoint within this transaction. |

---

### `ITransactionContext` (interface)

Provides access to the active database connection, transaction primitive, state, and savepoints during transactional execution.

```csharp
public interface ITransactionContext : IAsyncDisposable
```

| Member | Signature | Description |
|---|---|---|
| `TransactionId` | `Guid TransactionId { get; }` | Unique identifier of this transaction execution context. |
| `Connection` | `DbConnection Connection { get; }` | The underlying active database connection. |
| `Transaction` | `DbTransaction Transaction { get; }` | The underlying active database transaction. |
| `State` | `TransactionState State { get; }` | Current lifecycle state of the transaction. |
| `IsolationLevel` | `TransactionIsolationLevel IsolationLevel { get; }` | The isolation level configured for this transaction. |
| `CancellationToken` | `CancellationToken CancellationToken { get; }` | The cancellation token scoped to this transaction execution. |
| `Enlistments` | `IReadOnlyList<ITransactionEnlistment> Enlistments { get; }` | The list of enlistments attached to this transaction lifecycle. |
| `CreateSavepointAsync` | `Task<ISavepoint> CreateSavepointAsync(string name, CancellationToken cancellationToken = default)` | Creates a named savepoint within this transaction for partial rollback. |
| `Enlist` | `void Enlist(ITransactionEnlistment enlistment)` | Enlists a participant in the lifecycle notifications of this transaction. |

---

### `ISavepoint` (interface)

Represents a named savepoint within an active transaction, enabling partial rollback without aborting the outer transaction.

```csharp
public interface ISavepoint : IAsyncDisposable
```

| Member | Signature | Description |
|---|---|---|
| `Name` | `string Name { get; }` | The unique name of the savepoint. |
| `RollbackAsync` | `Task RollbackAsync(CancellationToken cancellationToken = default)` | Rolls back all operations since this savepoint was created. |
| `ReleaseAsync` | `Task ReleaseAsync(CancellationToken cancellationToken = default)` | Releases the savepoint in engines that support savepoint destruction. |

---

### `ITransactionEnlistment` (interface)

Defines lifecycle hooks for participants enlisting in a transaction boundary. All methods have default implementations returning `Task.CompletedTask`.

```csharp
public interface ITransactionEnlistment
```

| Member | Default | Description |
|---|---|---|
| `BeforeCommitAsync(ITransactionContext, CancellationToken)` | `Task.CompletedTask` | Executes immediately prior to committing the physical database transaction. |
| `AfterCommitAsync(ITransactionContext, CancellationToken)` | `Task.CompletedTask` | Executes immediately after the physical database transaction has committed successfully. |
| `AfterRollbackAsync(ITransactionContext, CancellationToken)` | `Task.CompletedTask` | Executes after the transaction has been rolled back. |
| `OnExceptionAsync(ITransactionContext, Exception, CancellationToken)` | `Task.CompletedTask` | Executes when an exception occurs during the execution or commit phase. Secondary exceptions are suppressed. |

---

### `IDbConnectionFactory` (interface)

Defines the contract for creating and opening database connections.

```csharp
public interface IDbConnectionFactory
```

| Member | Signature | Description |
|---|---|---|
| `CreateConnectionAsync` | `ValueTask<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)` | Creates and opens a new database connection. |
| `CreateConnection` | `DbConnection CreateConnection()` | Creates a new unopened database connection instance. |

---

### `TransactionOptions` (sealed record)

Immutable configuration options for controlling transaction behavior, isolation level, timeout, and nesting semantics.

```csharp
public sealed record TransactionOptions
```

| Member | Type / Default | Description |
|---|---|---|
| `IsolationLevel` | `TransactionIsolationLevel` (`ReadCommitted`) | Requested isolation level for the transaction. |
| `Timeout` | `TimeSpan?` (`null`) | Maximum duration before timeout. `null` uses the default driver timeout. |
| `ReadOnly` | `bool` (`false`) | Opens the transaction in read-only mode where supported by the provider. |
| `NestedBehavior` | `NestedTransactionBehavior` (`UseSavepoint`) | Behavior applied when nested inside an existing active transaction. |
| `TransactionName` | `string?` (`null`) | Optional logical name used in diagnostics and structured logging. |

**Static Factory Members:**

| Member | Description |
|---|---|
| `TransactionOptions.Default` | `ReadCommitted` isolation + `UseSavepoint` nesting. |
| `TransactionOptions.Serializable` | `Serializable` isolation + defaults. |
| `TransactionOptions.ReadOnlyMode` | `ReadOnly = true` + defaults. |
| `TransactionOptions.WithTimeout(TimeSpan)` | Creates a new instance with the specified timeout. |

---

### `TransactionState` (enum)

Specifies the lifecycle state of a transaction.

| Value | Integer | Description |
|---|---|---|
| `Created` | `0` | Instance created; physical transaction not yet begun. |
| `Active` | `1` | Transaction is actively executing and accepting operations. |
| `Committed` | `2` | Transaction has successfully committed all modifications. |
| `RolledBack` | `3` | Transaction has rolled back and all modified state was discarded. |
| `Failed` | `4` | Transaction encountered an unhandled error or ambiguous commit failure. |
| `Disposed` | `5` | Transaction has completed its lifecycle and released all resources. |

---

### `TransactionIsolationLevel` (enum)

Specifies the isolation level for a transaction.

| Value | Description |
|---|---|
| `ReadUncommitted` | Allows dirty reads. |
| `ReadCommitted` | Prevents dirty reads; allows non-repeatable reads. **(Default)** |
| `RepeatableRead` | Prevents dirty and non-repeatable reads. |
| `Serializable` | Prevents dirty reads, non-repeatable reads, and phantom reads. |
| `Snapshot` | MVCC row-versioning isolation (driver-dependent). |

---

### `NestedTransactionBehavior` (enum)

Specifies how the coordinator handles nested execution scopes when an ambient transaction is already active.

| Value | Description |
|---|---|
| `UseSavepoint` | Creates a named savepoint; rolls back only the nested scope on failure. **(Default)** |
| `RequireNew` | Suspends ambient context; opens an independent physical connection and transaction. |
| `Suppress` | Executes non-transactionally without ambient context enlistment. |
| `JoinExisting` | Enlists in the outer transaction without savepoints; any failure invalidates the entire transaction. |

---

### Exception Hierarchy

**Namespace**: `EricksonLopez.Transaction.Exceptions`

```mermaid
graph TD
    Ex["Exception (BCL)"]
    TEx["TransactionException : Exception\nBase for all transactional exceptions"]
    TCEx["TransactionCommitException : TransactionException\nIsAmbiguous : bool"]
    TREx["TransactionRollbackException : TransactionException"]
    TSEx["TransactionStateException : TransactionException"]
    TTEx["TransactionTimeoutException : TransactionException"]

    Ex --> TEx
    TEx --> TCEx
    TEx --> TREx
    TEx --> TSEx
    TEx --> TTEx
```

| Exception Type | Description | Key Property |
|---|---|---|
| `TransactionException` | Base class for all transaction-related exceptions. | — |
| `TransactionCommitException` | Thrown when a commit fails or the outcome is ambiguous. | `IsAmbiguous : bool` — `true` if the database engine may have committed despite the client-side error. |
| `TransactionRollbackException` | Thrown when a rollback operation fails. | — |
| `TransactionStateException` | Thrown when an invalid state transition is attempted. | — |
| `TransactionTimeoutException` | Thrown when a transaction exceeds its configured timeout. | — |

---

## Package: `EricksonLopez.Transaction`

Core engine implementing `ITransactionManager`, ambient context coordinator, state machine, and OpenTelemetry instrumentation.

**Namespace (DI extensions)**: `Microsoft.Extensions.DependencyInjection`

### `TransactionServiceCollectionExtensions` (static class)

Extension methods for registering transaction services into `IServiceCollection`. Registered with **Scoped** lifetime.

| Method | Description |
|---|---|
| `AddTransaction<TConnectionFactory>()` | Registers `TConnectionFactory` as `IDbConnectionFactory` and `TransactionManager` as `ITransactionManager`. |
| `AddTransaction(Func<IServiceProvider, IDbConnectionFactory>)` | Registers using a custom factory resolver delegate. |
| `AddTransaction(Func<IServiceProvider, CancellationToken, ValueTask<DbConnection>>)` | Registers using an async connection creation delegate (wrapped in `DelegateDbConnectionFactory`). |
| `AddTransaction(Func<IServiceProvider, DbConnection>)` | Registers using a synchronous connection creation delegate. |

### `DelegateDbConnectionFactory` (sealed class)

A concrete `IDbConnectionFactory` implementation that wraps delegate-based connection creation. Used internally by the `AddTransaction(Func<...>)` overloads but also available for direct instantiation.

> [!WARNING]
> When constructed with an **asynchronous** delegate (`Func<CancellationToken, ValueTask<DbConnection>>`), calling the synchronous `CreateConnection()` method throws `NotSupportedException`. Only `CreateConnectionAsync()` is supported in async-configured instances.

| Constructor | Description |
|---|---|
| `DelegateDbConnectionFactory(Func<DbConnection>)` | Synchronous delegate. Both `CreateConnection()` and `CreateConnectionAsync()` are supported. |
| `DelegateDbConnectionFactory(Func<CancellationToken, ValueTask<DbConnection>>)` | Asynchronous delegate. `CreateConnection()` throws `NotSupportedException`. |

---

### OpenTelemetry Instrumentation

Source name and meter name: **`"EricksonLopez.Transaction"`**

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("EricksonLopez.Transaction"))
    .WithMetrics(m => m.AddMeter("EricksonLopez.Transaction"));
```

**Registered Metric Instruments:**

| Instrument | Type | Unit | Description |
|---|---|---|---|
| `transactions.started` | Counter | `{transaction}` | Number of transactions started. |
| `transactions.committed` | Counter | `{transaction}` | Number of transactions committed successfully. |
| `transactions.rolled_back` | Counter | `{transaction}` | Number of transactions rolled back. |
| `transactions.failed` | Counter | `{transaction}` | Number of transactions that failed (including ambiguous commits). |
| `transactions.duration` | Histogram | `ms` | Duration of completed transactions in milliseconds. |
| `transactions.savepoints.created` | Counter | `{savepoint}` | Number of savepoints created. |
| `transactions.savepoints.rolled_back` | Counter | `{savepoint}` | Number of savepoints rolled back. |
| `transactions.savepoints.released` | Counter | `{savepoint}` | Number of savepoints released. |

**Distributed Tracing Tags (on `transaction.name` attribute):**

The `TransactionName` property from `TransactionOptions` is emitted as the `transaction.name` activity tag when set.

---

## Package: `EricksonLopez.Transaction.Dapper`

High-performance Dapper extension methods bound directly to `ITransactionContext`.

**Namespace**: `EricksonLopez.Transaction.Dapper`

### `TransactionDapperExtensions` (static class)

All methods are extension methods on `ITransactionContext`. All automatically bind `DbConnection`, `DbTransaction`, and `CancellationToken`.

| Method | Return Type | Description |
|---|---|---|
| `AsCommand(sql, param?, commandType?, flags?, commandTimeout?, ct?)` | `CommandDefinition` | Constructs an immutable `CommandDefinition` bound to the active transaction. Merges `context.CancellationToken` with the supplied token. |
| `ExecuteAsync(sql, param?, commandTimeout?, commandType?, ct?)` | `Task<int>` | Executes a SQL statement; returns rows affected. |
| `QueryAsync<T>(sql, param?, commandTimeout?, commandType?, ct?)` | `Task<IEnumerable<T>>` | Executes a query and returns mapped results. |
| `QuerySingleOrDefaultAsync<T>(sql, param?, commandTimeout?, commandType?, ct?)` | `Task<T?>` | Returns a single element or default if none found. |
| `QueryFirstOrDefaultAsync<T>(sql, param?, commandTimeout?, commandType?, ct?)` | `Task<T?>` | Returns the first element or default if none found. |
| `QuerySingleAsync<T>(sql, param?, commandTimeout?, commandType?, ct?)` | `Task<T>` | Returns a single element; throws if zero or more than one found. |
| `QueryFirstAsync<T>(sql, param?, commandTimeout?, commandType?, ct?)` | `Task<T>` | Returns the first element; throws if none found. |
| `ExecuteScalarAsync<T>(sql, param?, commandTimeout?, commandType?, ct?)` | `Task<T?>` | Returns the first column of the first row. |
| `QueryMultipleAsync(sql, param?, commandTimeout?, commandType?, ct?)` | `Task<SqlMapper.GridReader>` | Executes a multi-result-set query; returns a `GridReader` for sequential reading. |
| `ExecuteReaderAsync(sql, param?, commandTimeout?, commandType?, ct?)` | `Task<IDataReader>` | Executes a query and returns a raw `IDataReader`. |

---

## Package: `EricksonLopez.Transaction.Result`

Functional `Result<T>` monad integration with automatic rollback on failure.

**Namespace**: `EricksonLopez.Transaction.Result`

**Dependency**: `EricksonLopez.Result` (from the `dotnet-result` repository).

### `TransactionResultExtensions` (static class)

Extension methods on `ITransactionManager`. Automatically commit on `Result.IsSuccess` and rollback on `Result.IsFailure` — without requiring exception throwing.

| Method | Return Type | Description |
|---|---|---|
| `ExecuteResultAsync(Func<ITransactionContext, Task<Result>>, options?, ct?)` | `Task<Result>` | Executes a context-receiving operation returning a non-generic `Result`. |
| `ExecuteResultAsync(Func<Task<Result>>, options?, ct?)` | `Task<Result>` | Executes a parameterless operation returning a non-generic `Result`. |
| `ExecuteResultAsync<TValue>(Func<ITransactionContext, Task<Result<TValue>>>, options?, ct?)` | `Task<Result<TValue>>` | Executes a context-receiving operation returning a `Result<TValue>`. |
| `ExecuteResultAsync<TValue>(Func<Task<Result<TValue>>>, options?, ct?)` | `Task<Result<TValue>>` | Executes a parameterless operation returning a `Result<TValue>`. |

---

## Package: `EricksonLopez.Transaction.Testing`

In-memory test doubles for unit and integration testing without a real database.

**Namespace**: `EricksonLopez.Transaction.Testing`

### `FakeTransactionManager` (sealed class)

In-memory implementation of `ITransactionManager`.

| Member | Type | Description |
|---|---|---|
| `StartedTransactions` | `IReadOnlyList<FakeTransaction>` | All transactions created since the manager was constructed. |
| `ExceptionToThrowOnCommit` | `Exception?` | When set, commit operations on created transactions will throw this exception. |
| `CurrentContext` | `ITransactionContext?` | Can be set to simulate an active ambient context. |
| `BeginAsync(...)` | `Task<ITransaction>` | Creates a new `FakeTransaction` and adds it to `StartedTransactions`. |
| `ExecuteAsync(...)` | (all 4 overloads) | Creates a transaction, executes the operation, and commits. |

### `FakeTransaction` (sealed class)

In-memory implementation of `ITransaction`.

| Member | Type | Description |
|---|---|---|
| `TransactionId` | `Guid` | Unique identifier. |
| `Context` | `ITransactionContext` | The associated `FakeTransactionContext`. |
| `State` | `TransactionState` | Current lifecycle state. |
| `CommitCount` | `int` | Number of times `CommitAsync` was called. |
| `RollbackCount` | `int` | Number of times `RollbackAsync` was called. |
| `ExceptionToThrowOnCommit` | `Exception?` | When set, `CommitAsync` throws this exception. |
| `ExceptionToThrowOnRollback` | `Exception?` | When set, `RollbackAsync` throws this exception. Useful for testing rollback failure scenarios. |
| `IsDisposed` | `bool` | Returns `true` once the transaction has been disposed. Useful for verifying disposal in tests. |

### `FakeTransactionContext` (sealed class)

In-memory implementation of `ITransactionContext`.

| Member | Type | Description |
|---|---|---|
| `TransactionId` | `Guid` | Unique identifier. |
| `Connection` | `DbConnection` | Always throws `NotSupportedException`. `FakeTransactionContext` does not provide a physical database connection. |
| `Transaction` | `DbTransaction` | Always throws `NotSupportedException`. `FakeTransactionContext` does not provide a physical database transaction. |
| `State` | `TransactionState` | Mutable state for test assertions. |
| `IsolationLevel` | `TransactionIsolationLevel` | Configured isolation level. |
| `CancellationToken` | `CancellationToken` | Settable cancellation token for test scenarios. |
| `Enlistments` | `IReadOnlyList<ITransactionEnlistment>` | Registered enlistments. |
| `CreatedSavepoints` | `IReadOnlyList<string>` | Names of savepoints created on this context. Useful for assertions: `context.CreatedSavepoints.Should().HaveCount(1)`. |

---

## Dialect Provider Packages

Each dialect package provides a connection factory, an error classifier, and DI registration extensions.

### Registration Pattern

All provider packages expose DI extensions in the `Microsoft.Extensions.DependencyInjection` namespace:

| Package | DI Extension Method | Required Dependency |
|---|---|---|
| `EricksonLopez.Transaction.PostgreSql` | `services.AddPostgreSqlTransaction(NpgsqlDataSource)` | `NpgsqlDataSource` (singleton) |
| `EricksonLopez.Transaction.SqlServer` | `services.AddSqlServerTransaction(string connectionString)` | — |
| `EricksonLopez.Transaction.MySql` | `services.AddMySqlTransaction(string connectionString)` | — |
| `EricksonLopez.Transaction.MariaDb` | `services.AddMariaDbTransaction(string connectionString)` | — |
| `EricksonLopez.Transaction.Oracle` | `services.AddOracleTransaction(string connectionString)` | — |
| `EricksonLopez.Transaction.Sqlite` | `services.AddSqliteTransaction(string connectionString)` | — |

### Error Classifier API

Each dialect package exposes a static error classifier:

| Package | Classifier Type | Key Classified Errors |
|---|---|---|
| `EricksonLopez.Transaction.PostgreSql` | `PostgreSqlErrorClassifier` | SQLSTATE `40001` (serialization), `40P01` (deadlock), `25P02` (aborted transaction), `55P03` (lock timeout) |
| `EricksonLopez.Transaction.SqlServer` | `SqlServerErrorClassifier` | Error 1205 (deadlock), 3960/3961 (snapshot conflict), 1222 (lock timeout) |
| `EricksonLopez.Transaction.MySql` | `MySqlErrorClassifier` | Error 1213 (deadlock), 1205 (lock wait timeout) |
| `EricksonLopez.Transaction.MariaDb` | `MariaDbErrorClassifier` | Error 1213 (deadlock), 1205 (lock wait timeout) |
| `EricksonLopez.Transaction.Oracle` | `OracleErrorClassifier` | ORA-00060 (deadlock), ORA-08177 (serialization failure), ORA-30006 (lock timeout) |
| `EricksonLopez.Transaction.Sqlite` | `SqliteErrorClassifier` | `SQLITE_BUSY` (5), `SQLITE_LOCKED` (6) |

---

## Compatibility Matrix

| Package | net8.0 | net9.0 | net10.0 | Native AOT | Trimming Safe |
|---|:---:|:---:|:---:|:---:|:---:|
| `EricksonLopez.Transaction.Abstractions` | ✅ | ✅ | ✅ | ✅ | ✅ |
| `EricksonLopez.Transaction` | ✅ | ✅ | ✅ | ✅ | ✅ |
| `EricksonLopez.Transaction.Dapper` | ✅ | ✅ | ✅ | ✅ | ✅ |
| `EricksonLopez.Transaction.PostgreSql` | ✅ | ✅ | ✅ | ✅ | ✅ |
| `EricksonLopez.Transaction.SqlServer` | ✅ | ✅ | ✅ | ✅ | ✅ |
| `EricksonLopez.Transaction.MySql` | ✅ | ✅ | ✅ | ✅ | ✅ |
| `EricksonLopez.Transaction.MariaDb` | ✅ | ✅ | ✅ | ✅ | ✅ |
| `EricksonLopez.Transaction.Oracle` | ✅ | ✅ | ✅ | ✅ | ✅ |
| `EricksonLopez.Transaction.Sqlite` | ✅ | ✅ | ✅ | ✅ | ✅ |
| `EricksonLopez.Transaction.Result` | ✅ | ✅ | ✅ | ✅ | ✅ |
| `EricksonLopez.Transaction.Testing` | ✅ | ✅ | ✅ | ✅ | ✅ |

> All packages enforce `<IsAotCompatible>true</IsAotCompatible>` and `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>` via `Directory.Build.props`. AOT compatibility is validated by the `EricksonLopez.Transaction.AotSmokeTest` binary executed in the CI pipeline (`aot-smoke-test.yml`).
