# Changelog

All notable changes to `EricksonLopez.Transaction` will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.0.0] - 2026-09-23

### Breaking Changes
- **BC-001 (`ITransactionContext` Interface Expansion)**: Added `bool IsRollbackOnly { get; }` and `void SetRollbackOnly(string reason);` to public interface `ITransactionContext` without default interface implementations.
  - *Previous*: `ITransactionContext` only contained connection, transaction, state, isolation level, cancellation token, and enlistment operations.
  - *Current*: All implementations of `ITransactionContext` must provide `IsRollbackOnly` and `SetRollbackOnly(string)`.
  - *Affected Consumers*: Third-party classes and test doubles implementing `ITransactionContext`. Fails compilation with `CS0535`.
  - *Migration*: Implement the two new members on your custom `ITransactionContext` implementation, or use `FakeTransactionContext` from `EricksonLopez.Transaction.Testing`.

- **BC-002 (`PostgreSqlConnectionFactory.CreateConnection` Return State)**: Changed the connection state returned by synchronous `CreateConnection()` from `Open` to `Closed` (unopened).
  - *Previous*: Called `_dataSource.OpenConnection()`, returning a connection in `ConnectionState.Open`.
  - *Current*: Calls `_dataSource.CreateConnection()`, returning an unopened connection in `ConnectionState.Closed` in strict compliance with `IDbConnectionFactory.CreateConnection()`.
  - *Affected Consumers*: Code invoking synchronous `factory.CreateConnection()` and immediately issuing SQL queries or commands without opening the connection first.
  - *Migration*: Call `await factory.CreateConnectionAsync(ct)` to obtain an open connection asynchronously, or call `connection.Open()` before executing commands on the connection returned by `CreateConnection()`.

- **BC-003 (`TransactionPostCommitException` Exception Hierarchy Shift)**: Failures occurring in `AfterCommitAsync` enlistment hooks now throw `TransactionPostCommitException` rather than `TransactionCommitException`.
  - *Previous*: Post-commit hook exceptions were caught in the primary commit try-block, throwing `TransactionCommitException` with `isAmbiguous: true` and marking the transaction as `Failed`.
  - *Current*: Physical commit persists successfully, leaving the state machine in `TransactionState.Committed`. Subsequent post-commit hook failures throw `TransactionPostCommitException` (which derives from `TransactionException`, NOT `TransactionCommitException`).
  - *Affected Consumers*: Consumers wrapping `CommitAsync()` or `ExecuteAsync()` in `catch (TransactionCommitException)`.
  - *Migration*: Add a catch handler for `TransactionPostCommitException` prior to `TransactionCommitException`. Do not attempt database rollbacks in this handler because physical database persistence has already irrevocably succeeded.

- **BC-004 (`TransactionCommitException.IsAmbiguous` Semantics Refinement)**: Pre-dispatch commit failures now set `IsAmbiguous = false`.
  - *Previous*: Any exception during `CommitAsync` unconditionally set `isAmbiguous: true`.
  - *Current*: Failures during `BeforeCommitAsync` hooks or cancellation before the commit command is dispatched across the database socket set `isAmbiguous: false`. Only socket-level dispatch failures set `isAmbiguous: true`.
  - *Affected Consumers*: Custom retry policies or Polly policies inspecting `ex.IsAmbiguous`.
  - *Migration*: Review reconciliation policies. Pre-dispatch exceptions (`IsAmbiguous == false`) should be handled via normal transient retry, whereas post-dispatch ambiguous outcomes (`IsAmbiguous == true`) require idempotency verification.

- **BC-005 (`NestedTransactionBehavior.JoinExisting` Parent Rollback Enforcement)**: Nested `JoinExisting` scopes now mark parent transactions as rollback-only upon failure or uncommitted disposal.
  - *Previous*: Inner `JoinExisting` scope rollbacks were local to the inner scope adapter; parent transactions could still commit successfully.
  - *Current*: If an inner `JoinExisting` scope calls `RollbackAsync()` or is disposed without committing, it sets `IsRollbackOnly = true` on the parent context. When the parent calls `CommitAsync()`, it aborts, rolls back physically, and throws `TransactionCommitException`.
  - *Affected Consumers*: Workflows relying on inner `JoinExisting` scope exceptions being swallowed to allow the outer transaction to commit partial work.
  - *Migration*: If an inner operation must allow isolated failure without rolling back the enclosing transaction, configure its options with `NestedTransactionBehavior.UseSavepoint` instead of `JoinExisting`.

- **BC-006 (`Savepoint` Name ASCII and 128-Character Validation)**: Enforced strict alphanumeric ASCII formatting and 128-character limit on savepoint identifiers.
  - *Previous*: `char.IsLetterOrDigit` permitted Unicode characters from non-Latin scripts, with no maximum length restriction.
  - *Current*: Only ASCII characters `[a-zA-Z0-9_]` with a maximum length of 128 characters are accepted. Invalid inputs throw `ArgumentException`.
  - *Affected Consumers*: Systems dynamically generating savepoint names containing Unicode characters or strings exceeding 128 characters.
  - *Migration*: Restrict custom savepoint names to ASCII letters, numbers, and underscores, truncated to 128 characters.

- **BC-007 (`TransactionContext` Post-Disposal Property Guards)**: `ITransactionContext.Connection` and `ITransactionContext.Transaction` throw `ObjectDisposedException` when accessed after disposal.
  - *Previous*: Properties returned the underlying objects even after context disposal.
  - *Current*: Accessing `Connection` or `Transaction` after `DisposeAsync()` throws `ObjectDisposedException`.
  - *Affected Consumers*: Cleanup routines, background logging, or callbacks accessing context properties outside the active transaction boundary.
  - *Migration*: Inspect or capture connection/transaction references only while the transaction scope is active.

- **BC-008 (`TransactionalAttribute` Decoupled from Runtime Pipeline)**: `[TransactionalAttribute]` no longer configures isolation level or timeout in `TransactionPipelineBehavior`.
  - *Previous*: Reflection was used to inspect `[Transactional]` on mediator request classes to apply custom `TransactionOptions`.
  - *Current*: Reflection was eliminated to guarantee 100% Native AOT trimming compatibility. `[Transactional]` is now a metadata marker only. `TransactionPipelineBehavior` solely inspects `ITransactionalCommandOptions`.
  - *Affected Consumers*: Mediator commands relying on `[Transactional(IsolationLevel, TimeoutSeconds)]` to set non-default transaction options.
  - *Migration*: Implement `ITransactionalCommandOptions` on your command class to supply `TransactionOptions` functionally at runtime.

- **BC-009 (`FakeTransaction` Post-Disposal Operations Guarded)**: `FakeTransaction.CommitAsync()`, `RollbackAsync()`, and `CreateSavepointAsync()` throw `ObjectDisposedException` after disposal.
  - *Previous*: Method calls succeeded silently on disposed fake transaction instances.
  - *Current*: Invoking lifecycle operations on a disposed `FakeTransaction` throws `ObjectDisposedException`.
  - *Affected Consumers*: Test suites asserting or executing operations on disposed fake transactions.
  - *Migration*: Update test assertions to invoke lifecycle operations prior to disposing the `FakeTransaction` instance.

- **BC-010 (`TransactionManager` Constructor Binary Signature)**: Added optional `IEnumerable<IDatabaseDialect>? dialects = null` parameter to public constructor.
  - *Previous*: Constructor accepted `(IDbConnectionFactory, ILogger<TransactionManager>?)`.
  - *Current*: Constructor signature is `(IDbConnectionFactory, ILogger<TransactionManager>?, IEnumerable<IDatabaseDialect>?)`.
  - *Affected Consumers*: Pre-compiled third-party assemblies calling `new TransactionManager(...)` without recompilation.
  - *Migration*: Recompile consuming assemblies against the updated package. (Source code compatibility is preserved).

- **BC-011 (`FakeTransaction` Constructor Binary Signature)**: Added optional `Action? onDisposed = null` parameter to public constructor.
  - *Previous*: Constructor accepted `(FakeTransactionContext?)`.
  - *Current*: Constructor signature is `(FakeTransactionContext?, Action?)`.
  - *Affected Consumers*: Pre-compiled third-party assemblies calling `new FakeTransaction(...)` without recompilation.
  - *Migration*: Recompile consuming test projects against the updated package. (Source code compatibility is preserved).

- **BC-012 (`ConnectionManipulationAnalyzer` Severity Error Build Failure)**: Added Roslyn Analyzer `ELT001` configured with `DiagnosticSeverity.Error` by default.
  - *Previous*: No analyzer was present.
  - *Current*: Code that directly calls `.Close()`, `.Dispose()`, `.DisposeAsync()`, `.ChangeDatabase()`, `.BeginTransaction()`, or `.BeginTransactionAsync()` on `ITransactionContext.Connection` will trigger compile-time error `ELT001`.
  - *Affected Consumers*: Projects consuming `EricksonLopez.Transaction.Analyzers`.
  - *Migration*: Remove manual connection lifecycle mutations and let `TransactionManager` handle connection state. If unavoidable, suppress `ELT001` via `#pragma warning disable ELT001`.

- **BC-013 (`EntityFrameworkCore` and `Resilience` Target Framework Restriction)**: Packages `EricksonLopez.Transaction.EntityFrameworkCore` and `EricksonLopez.Transaction.Resilience` target `.NET 10.0` exclusively.
  - *Previous*: Core transaction packages target `net8.0;net9.0;net10.0`.
  - *Current*: EF Core and Resilience packages do not provide builds for `net8.0` or `net9.0`.
  - *Affected Consumers*: Applications targeting .NET 8.0 or .NET 9.0.
  - *Migration*: Target project must be upgraded to `.NET 10.0` to reference EF Core and Resilience integration packages.

- **BC-014 (`TransactionStateMachine` Committed State Locking)**: `TransactionStateMachine.TransitionToFailed()` is a no-op once state is `Committed`.
  - *Previous*: Calling `TransitionToFailed()` on a committed transaction changed its state to `Failed`.
  - *Current*: A committed transaction remains permanently in `TransactionState.Committed`.
  - *Affected Consumers*: Custom transaction coordinators or tests expecting state transitions to `Failed` after physical commit.
  - *Migration*: Inspect thrown exceptions (`TransactionPostCommitException`) rather than querying `transaction.State` to detect post-commit hook failures.

### Added
- **Ecosystem Integrations**:
  - `EricksonLopez.Transaction.EntityFrameworkCore`: `DbContextTransactionExtensions.UseTransactionAsync` connecting EF Core `DbContext` to ambient `ITransactionContext`.
  - `EricksonLopez.Transaction.Mediator`: `TransactionPipelineBehavior<TRequest, TResponse>` with `ITransactionalCommandOptions` for runtime transaction options, `[Transactional]` as a documentation marker, and `AddTransactionPipelineBehavior` DI registration.
  - `EricksonLopez.Transaction.Resilience`: `PollyTransactionExtensions.HandleAmbiguousCommit` policy builder extension for transient retry and idempotency reconciliation around ambiguous commit boundaries.
  - `EricksonLopez.Transaction.Analyzers`: Roslyn Analyzer `ELT001` (`ConnectionManipulationAnalyzer`) enforcing safe connection lifecycle usage and preventing direct manipulation of `ITransactionContext.Connection`.
- **Abstractions & Dialect Architecture**:
  - `IDatabaseDialect`: Interface abstracting database-specific savepoint syntax (`GetSavepointCreationSql`, `GetSavepointRollbackSql`, `GetSavepointReleaseSql`) and error classifiers across engines.
  - `TransactionPostCommitException`: Dedicated exception for enlistment `AfterCommitAsync` failures occurring after physical commit persistence.
  - `TransactionOptions.SanitizeTelemetryMetadata`: When enabled, redacts `transaction.id` and `transaction.name` tags in OpenTelemetry activity spans.
  - `ITransactionContext.IsRollbackOnly` and `SetRollbackOnly(string reason)`: Explicit rollback-only marking on active transaction contexts.
- **Showcase Reference Implementation (`samples/Showcase`)**:
  - 11-level progressive executable showcase covering Conceptual Foundations (Level 00) through Enterprise Outbox & Idempotency Architecture (Level 10).
  - 100% Native AOT compliance and automated non-interactive execution mode (`--all`).
- **Architectural Decision Records**:
  - `ADR-027` through `ADR-031` documenting EF Core transaction enlistment, Polly resilience integration, Roslyn connection analyzer, pluggable database dialects, and Oracle dialect specification.
  - `ADR-032`: `[TransactionalAttribute]` as metadata marker — no runtime effect in `TransactionPipelineBehavior`. `ITransactionalCommandOptions` is the sole functional mechanism.
  - `ADR-033`: `SanitizeTelemetryMetadata` PII redaction scope — `transaction.id` and `transaction.name` span tag redaction only. SQL parameter redaction is out of scope.

### Changed
- **`TransactionOptions.SanitizeTelemetryMetadata` is now functionally implemented**: Previously this property existed in the API surface but was never read by the implementation. It now correctly redacts `transaction.id` and `transaction.name` from OpenTelemetry spans when enabled. This closes a false security guarantee identified in an exhaustive coherence audit.
- **`[TransactionalAttribute]` XML documentation corrected**: The attribute is now explicitly documented as a metadata marker with no runtime effect. `ITransactionalCommandOptions` is documented as the functional mechanism.
- **`docs/architecture.md` Mediator sequence diagram corrected**: Removed the phantom `Inspect [Transactional] Attribute` step. The diagram now accurately shows `Check ITransactionalCommandOptions (cast, no reflection)`.
- **README log messages corrected**: Two real `[LoggerMessage]` events are now documented (`SuppressedScopeBeginning`, `TransactionTimeoutExceeded`). Three fictitious log messages removed.
- **CHANGELOG multi-targeting claim corrected**: `EntityFrameworkCore` and `Resilience` target `.NET 10.0` exclusively.
- **`TransactionOptions.Default` singleton behavior documented**: `Default` is a zero-allocation shared instance. `Serializable` and `ReadOnlyMode` are properties that create new instances on each access.
- **ADR-003 savepoint naming corrected**: Format is `sp_` + 24 lowercase hex chars (first 24 of `Guid.NewGuid().ToString("N")`), not `sp_{Guid}`.
- **ADR-004 `IsAmbiguous` semantics corrected**: `IsAmbiguous = true` only when commit was dispatched to the database engine socket. Pre-dispatch failures set `IsAmbiguous = false`.

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
  - Multi-targeting for `.NET 8.0`, `.NET 9.0`, and `.NET 10.0` across core solution packages (`Abstractions`, `Transaction`, `Mediator`, `Result`, `Dapper`, `Testing`, `Analyzers`, and all dialect providers). **`EricksonLopez.Transaction.EntityFrameworkCore`** and **`EricksonLopez.Transaction.Resilience`** target `.NET 10.0` exclusively.
  - Native AOT trimming compatibility enforced with `<IsAotCompatible>true</IsAotCompatible>` and verified by `EricksonLopez.Transaction.AotSmokeTest`.
  - Strong-name signing across all assemblies (`SignAssembly=true`) using `EricksonLopez.snk`.
  - Architecture Decision Records (`ADR-001` through `ADR-026`) documenting transactional semantics, error classification, telemetry invariants, and architectural guardrails.

---

[Unreleased]: https://github.com/ericksonlopezf/dotnet-transaction/compare/v2.0.0...HEAD
[2.0.0]: https://github.com/ericksonlopezf/dotnet-transaction/compare/v1.0.0...v2.0.0
[1.0.0]: https://github.com/ericksonlopezf/dotnet-transaction/releases/tag/v1.0.0
