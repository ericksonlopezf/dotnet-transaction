# Public API Inventory

This document provides a comprehensive inventory of public types, interfaces, options, and extension methods exported by the `EricksonLopez.Transaction` package ecosystem.

---

## 1. `EricksonLopez.Transaction.Abstractions`

### Core Interfaces
- `ITransactionManager`: Primary contract for initiating, committing, rolling back, and orchestrating database transactions.
- `ITransaction`: Represents an active transaction unit implementing `IAsyncDisposable`.
- `ITransactionContext`: Execution context containing the active `DbConnection`, `DbTransaction`, and ambient metadata.
- `ISavepoint`: Contract representing an active named transactional savepoint.
- `IDbConnectionFactory`: Contract for creating asynchronous ADO.NET database connections.
- `ITransactionEnlistment`: Contract for participating in two-phase commit or transactional lifecycle events.

### Options & Enums
- `TransactionOptions`: Configuration model defining `TransactionIsolationLevel`, `NestedTransactionBehavior`, and timeout limits.
- `TransactionIsolationLevel`: Enum specifying transaction isolation (`ReadUncommitted`, `ReadCommitted`, `RepeatableRead`, `Serializable`, `Snapshot`).
- `NestedTransactionBehavior`: Enum defining nested execution scope handling (`UseSavepoint`, `RequireNew`, `Suppress`, `JoinExisting`).
- `TransactionState`: Enum describing the transaction lifecycle (`Active`, `Committed`, `RolledBack`, `Disposed`).

### Exceptions
- `TransactionException`: Base exception for all transaction coordinator faults.
- `TransactionCommitException`: Thrown when committing a transaction fails or enters an uncertain state.
- `TransactionRollbackException`: Thrown when rolling back a transaction encounters an ADO.NET failure.
- `TransactionStateException`: Thrown when attempting an invalid state transition (e.g. committing a rolled-back transaction).
- `TransactionTimeoutException`: Thrown when a transaction exceeds its configured execution timeout.

---

## 2. `EricksonLopez.Transaction` (Core)

### Implementation & Internal Scope Management
- `TransactionManager`: Production coordinator implementing `ITransactionManager` with ambient async-local context propagation.
- `DelegateDbConnectionFactory`: Lightweight `IDbConnectionFactory` delegating connection creation to a provider factory func.
- `TransactionDiagnostics`: OpenTelemetry distributed tracing and metrics provider for transaction lifecycles.

### Dependency Injection
- `TransactionServiceCollectionExtensions`: Extensions for registering transaction management services (`AddTransactionManagement()`).

---

## 3. `EricksonLopez.Transaction.Dapper`

### Functional Extensions
- `TransactionDapperExtensions`: Direct extensions on `ITransactionContext` providing frictionless `ExecuteAsync`, `QueryAsync`, and `QuerySingleOrDefaultAsync` over Dapper.

---

## 4. `EricksonLopez.Transaction.Result`

### Functional Extensions
- `TransactionResultExtensions`: Overloads of `ExecuteAsync` taking delegates returning `Result<T>` that automatically trigger `RollbackAsync` on `Result.Failure`.

---

## 5. `EricksonLopez.Transaction.Testing`

### Test Doubles & Fakes
- `FakeTransactionManager`: In-memory implementation of `ITransactionManager` with call history and simulated failure injectors.
- `FakeTransactionContext`: In-memory transaction context for unit tests without database engines.
- `FakeTransaction`: Mock-free `ITransaction` fake recording commit and rollback invocations.

---

## 6. Provider Extensions

- `EricksonLopez.Transaction.PostgreSql`: PostgreSQL connection factories and SQLSTATE classifiers (`40001`, `40P01`).
- `EricksonLopez.Transaction.SqlServer`: SQL Server connection factories and error classifiers (`1205`).
- `EricksonLopez.Transaction.MySql`: MySQL connection factories and error classifiers.
- `EricksonLopez.Transaction.MariaDb`: MariaDB connection factories and error classifiers.
- `EricksonLopez.Transaction.Oracle`: Oracle connection factories and ORA error classifiers.
- `EricksonLopez.Transaction.Sqlite`: SQLite connection factories and error classifiers.
