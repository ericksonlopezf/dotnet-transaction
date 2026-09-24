# EricksonLopez.Transaction — Interactive Showcase

[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](../../LICENSE)
[![Native AOT](https://img.shields.io/badge/Native%20AOT-Compatible-success.svg)](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
[![Showcase](https://img.shields.io/badge/Showcase-11%2F11%20Levels%20Passing-brightgreen.svg)](#-level-catalog)

Official executable reference implementation, interactive catalog, and architectural guide for **`EricksonLopez.Transaction`**.

---

## Overview

The **Showcase** is the **executable documentation** for `EricksonLopez.Transaction`. Every public API across the Core and Infrastructure libraries is demonstrated across **11 progressive educational levels** (Levels 00-10).

It guarantees:
- **Zero API obsolescence**: Showcase only uses APIs that exist in the library.
- **Complete API surface coverage**: Every public class, interface, method, and enum value is demonstrated.
- **Runtime correctness**: All 11 levels execute to completion with exit code 0.
- **AOT-safe patterns**: Examples cover both reflection-based and AOT-safe alternatives.
- **Authentic scenarios**: Real-world business patterns (Outbox, Idempotency, Clean Architecture, Sagas).

---

## Running the Showcase

### Run All Levels (Batch Mode)
```bash
dotnet run --project samples/Showcase/EricksonLopez.Transaction.Showcase.csproj -- --all
```

### Run a Single Level
```bash
dotnet run --project samples/Showcase/EricksonLopez.Transaction.Showcase.csproj -- --level 5
```

### Interactive Console Menu
```bash
dotnet run --project samples/Showcase/EricksonLopez.Transaction.Showcase.csproj
```

**Requirements:** .NET 10.0 SDK. No external database required - all examples use SQLite in-memory.

---

## Level Catalog

| Level | Title | Category |
|---|---|---|
| **00** | Conceptual & Architectural Foundations | Conceptual |
| **01** | Quick Start & Minimal Setup | Beginner |
| **02** | Complete Configuration & TransactionOptions | Configuration |
| **03** | Real-World Business Use Cases & Explicit Lifecycles | Intermediate |
| **04** | Dapper Extensions & Result<T> Monad Integration | Integration |
| **05** | Nested Transactions, Savepoints & All NestedTransactionBehavior Modes | Advanced |
| **06** | Error Handling, Commit Ambiguity & Error Classifiers | Resilience |
| **07** | Scalability, Concurrency & OpenTelemetry Observability | Enterprise |
| **08** | Extensibility, Custom Enlistments & In-Memory Test Doubles | Extensibility |
| **09** | Multi-DB Dialect Providers, Factories & Mediator Attributes | Dialects |
| **10** | Enterprise Architecture: Dual-Write Outbox & Idempotency | Architecture |

---

### Level 00 - Conceptual & Architectural Foundations
Problems with raw DbTransaction and TransactionScope/MSDTC. Core design invariants.
TransactionState enum (all 6 values). Comparative capabilities matrix.

### Level 01 - Quick Start & Minimal Setup
DI registration with AddTransaction / AddSqliteTransaction. First atomic multi-step balance transfer.
ITransactionManager.ExecuteAsync. ITransactionContext.ExecuteAsync (Dapper DML).

### Level 02 - Complete Configuration & TransactionOptions
All 4 AddTransaction DI overloads. TransactionOptions record (all 6 properties).
Static factory members: Default, Serializable, ReadOnlyMode, WithTimeout.
TransactionIsolationLevel enum (6 values). NestedTransactionBehavior enum (4 values).

### Level 03 - Real-World Business Use Cases & Explicit Lifecycles
Multi-repository coordination in Clean Architecture. ITransactionManager.BeginAsync.
ITransaction.CommitAsync, RollbackAsync, State, TransactionId, Context.
Automatic rollback on DisposeAsync without CommitAsync.

### Level 04 - Dapper Extensions & Result<T> Monad Integration
All 10 TransactionDapperExtensions methods: AsCommand, ExecuteAsync, QueryAsync<T>,
QueryFirstOrDefaultAsync<T>, QuerySingleOrDefaultAsync<T>, QueryFirstAsync<T>,
QuerySingleAsync<T>, ExecuteScalarAsync<T>, QueryMultipleAsync, ExecuteReaderAsync.
All 4 TransactionResultExtensions.ExecuteResultAsync overloads (generic/non-generic, with/without context).

### Level 05 - Nested Transactions, Savepoints & All NestedTransactionBehavior Modes
Hierarchical ISavepoint isolation. Direct ISavepoint API: CreateSavepointAsync, Name, RollbackAsync, ReleaseAsync.
Ambient AsyncLocal context propagation. All 4 NestedTransactionBehavior values:
UseSavepoint (partial rollback), JoinExisting (shared physical tx), RequireNew (independent tx), Suppress (no tx).

### Level 06 - Error Handling, Commit Ambiguity & Error Classifiers
Complete exception hierarchy:
  Exception -> TransactionException (base)
    |- TransactionCommitException (IsAmbiguous - idempotency reconciliation)
    |- TransactionPostCommitException (DB committed - do NOT rollback)
    |- TransactionRollbackException (rollback operation failed)
    |- TransactionStateException (invalid lifecycle transition - ActualState, AttemptedOperation)
    |- TransactionTimeoutException (timeout exceeded - Timeout property)
TransactionException as polymorphic catch target. All exception constructors demonstrated.
FakeTransactionManager.ExceptionToThrowOnCommit for test doubles.
All 6 dialect error classifiers: PostgreSql, SqlServer, MySql, MariaDb, Oracle, Sqlite.
PostgreSqlErrorClassifier: 8 SQLSTATE constants + IsSerializationFailure, IsDeadlock, IsInFailedTransaction, IsTransient.

### Level 07 - Scalability, Concurrency & OpenTelemetry Observability
TransactionDiagnostics complete public API: SourceName, Version, ActivitySource, Meter,
StartActivity, RecordStarted, RecordCommitted, RecordRolledBack, RecordFailed,
RecordSavepointCreated, RecordSavepointRolledBack, RecordSavepointReleased.
8 emitted metric names. 50 concurrent parallel transactions. Native AOT zero-reflection invariants.

### Level 08 - Extensibility, Custom Enlistments & In-Memory Test Doubles
DelegateDbConnectionFactory (async + sync constructors), IDbConnectionFactory.CreateConnection/CreateConnectionAsync.
All 4 ITransactionEnlistment hooks: BeforeCommitAsync, AfterCommitAsync, AfterRollbackAsync, OnExceptionAsync.
ITransactionContext.Enlist, IsRollbackOnly, SetRollbackOnly.
FakeTransactionManager, FakeTransaction, FakeTransactionContext - complete public APIs.
IDatabaseDialect custom implementation. TransactionManager(factory, logger, dialects) constructor.

### Level 09 - Multi-DB Dialect Providers, Factories & Mediator Attributes
6 dialect DI extensions: AddPostgreSqlTransaction, AddSqlServerTransaction, AddMySqlTransaction,
AddMariaDbTransaction, AddOracleTransaction, AddSqliteTransaction.
AddTransactionPipelineBehavior, ITransactionalCommand, TransactionalAttribute (reflection-based),
ITransactionalCommandOptions (AOT-safe), TransactionPipelineBehavior.Handle.
PollyTransactionExtensions: HandleAmbiguousCommit() static + extension.
DbContextTransactionExtensions.UseTransactionAsync (EF Core binding).

### Level 10 - Enterprise Architecture: Dual-Write Outbox & Idempotency
Transactional Outbox pattern: Order + outbox message written atomically in one ExecuteAsync.
Idempotency key protection within the same DbTransaction.
Clean Architecture layer ownership rules.

---

## Project Structure

```
samples/Showcase/
  EricksonLopez.Transaction.Showcase.csproj
  Program.cs                      # Entry point, level registry, CLI arg parsing
  ILevel.cs                       # Interface contract for all levels
  README.md                       # This document
  Levels/
    Level0_Architecture.cs
    Level1_GettingStarted.cs
    Level2_Configuration.cs
    Level3_RealUseCases.cs
    Level4_AdvancedIntegration.cs
    Level5_Processing.cs
    Level6_ErrorHandling.cs
    Level7_Scalability.cs
    Level8_Customization.cs
    Level9_Extensions.cs
    Level10_EnterpriseArchitecture.cs
```

---

## Packages Referenced

| Package | Purpose |
|---|---|
| EricksonLopez.Transaction.Abstractions | Core interfaces and value objects |
| EricksonLopez.Transaction | TransactionManager, DelegateDbConnectionFactory, TransactionDiagnostics |
| EricksonLopez.Transaction.Dapper | Dapper ITransactionContext extension methods |
| EricksonLopez.Transaction.Result | ExecuteResultAsync Result monad integration |
| EricksonLopez.Transaction.Testing | FakeTransactionManager, FakeTransaction, FakeTransactionContext |
| EricksonLopez.Transaction.PostgreSql | Npgsql factory, PostgreSqlErrorClassifier |
| EricksonLopez.Transaction.SqlServer | SqlClient factory, SqlServerErrorClassifier |
| EricksonLopez.Transaction.MySql | MySqlConnector factory, MySqlErrorClassifier |
| EricksonLopez.Transaction.MariaDb | MySqlConnector MariaDB factory, MariaDbErrorClassifier |
| EricksonLopez.Transaction.Oracle | ODP.NET factory, OracleErrorClassifier |
| EricksonLopez.Transaction.Sqlite | Microsoft.Data.Sqlite factory, SqliteErrorClassifier |
| EricksonLopez.Transaction.EntityFrameworkCore | DbContextTransactionExtensions.UseTransactionAsync |
| EricksonLopez.Transaction.Mediator | TransactionPipelineBehavior, ITransactionalCommand, TransactionalAttribute |
| EricksonLopez.Transaction.Resilience | PollyTransactionExtensions.HandleAmbiguousCommit |

---

## Invariants & Governance Rules

- **100% Public API Fidelity**: Only APIs that exist in Core & Infrastructure packages are demonstrated.
- **Zero Fictional APIs**: No simulated methods, no non-existent extensions, no hypothetical behavior.
- **Executable Reference**: Every level compiles and executes to completion with exit code 0.
- **Progressive Pedagogy**: Each level builds on the previous, forming a complete learning path.
- **Synchronization Mandate**: Every change to the library must be reflected in the Showcase before merging.

*Copyright (c) Erickson Lopez. MIT License.*
