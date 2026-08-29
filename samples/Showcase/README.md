# EricksonLopez.Transaction — Interactive Showcase

[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-purple.svg)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](../../LICENSE)
[![Native AOT](https://img.shields.io/badge/Native%20AOT-Compatible-success.svg)](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)

Official executable reference implementation, interactive catalog, and architectural guide for **`EricksonLopez.Transaction`**.

---

## 📖 Overview

The **Showcase** serves as the executable documentation for `EricksonLopez.Transaction`. Every public API in the Core and Infrastructure libraries is demonstrated across **11 progressive educational levels** (Levels 00 through 10).

It ensures zero API obsolescence, complete compilability, and authentic runtime validation across all transaction boundaries, Savepoint hierarchies, Result monad failure handling, and multi-database dialect error classifications.

---

## 🚀 Running the Showcase

### Run All Levels in Batch Mode
```bash
dotnet run --project samples/Showcase/EricksonLopez.Transaction.Showcase.csproj --framework net10.0 -- --all
```

### Run a Specific Level
```bash
dotnet run --project samples/Showcase/EricksonLopez.Transaction.Showcase.csproj --framework net10.0 -- --level 3
```

### Interactive Console Menu
```bash
dotnet run --project samples/Showcase/EricksonLopez.Transaction.Showcase.csproj --framework net10.0
```

---

## 📚 Level Catalog

| Level | Title | Category | Description & Key APIs |
|---|---|---|---|
| **Level 00** | **Conceptual & Architectural Foundations** | `Conceptual` | Problems with raw `DbTransaction` & `TransactionScope`; Core design invariants; Comparative capabilities matrix. |
| **Level 01** | **Quick Start & Minimal Setup** | `Beginner` | DI setup with `AddTransaction` / `AddSqliteTransaction`; First atomic multi-step balance transfer with `ExecuteAsync`. |
| **Level 02** | **Complete Configuration & TransactionOptions** | `Configuration` | `TransactionOptions` customization, Isolation Levels (`ReadCommitted`, `Serializable`, `Snapshot`), `WithTimeout`, `ReadOnlyMode`. |
| **Level 03** | **Real-World Business Use Cases & Explicit Lifecycles** | `Intermediate` | Multi-repository coordination in Clean Architecture; Explicit lifecycle control via `BeginAsync`, `CommitAsync`, and automatic rollback on disposal. |
| **Level 04** | **Dapper Extensions & Result&lt;T&gt; Monad Integration** | `Integration` | `TransactionDapperExtensions` (`AsCommand`, `QueryAsync`, `ExecuteScalarAsync`, `QueryMultipleAsync`, `ExecuteReaderAsync`) and `ExecuteResultAsync` with automatic rollback on `Result.Failure`. |
| **Level 05** | **Nested Transactions, Savepoints & Ambient Context Flow** | `Advanced` | Hierarchical `ISavepoint` isolation (`UseSavepoint`), suppressed scopes (`Suppress`), partial error recovery, and `AsyncLocal` ambient context propagation. |
| **Level 06** | **Error Handling, Commit Ambiguity & Error Classifiers** | `Resilience` | Handling `TransactionCommitException` with `IsAmbiguous = true`, `TransactionTimeoutException`, and 6 dialect error classifiers. |
| **Level 07** | **Scalability, Concurrency & OpenTelemetry Observability** | `Enterprise` | 50 concurrent transactions, distributed tracing with `ActivitySource`, metric telemetry with `Meter`, and Native AOT zero-reflection invariants. |
| **Level 08** | **Extensibility, Custom Enlistments & In-Memory Test Doubles** | `Extensibility` | `DelegateDbConnectionFactory`, `ITransactionEnlistment` hooks (`BeforeCommit`, `AfterCommit`, `AfterRollback`, `OnException`), and testing via `FakeTransactionManager`. |
| **Level 09** | **Multi-DB Dialect Providers & Connection Factories** | `Dialects` | PostgreSQL, SQL Server, MySQL, MariaDB, Oracle, SQLite connection factories and DI registration extensions. |
| **Level 10** | **Enterprise Architecture: Dual-Write Outbox & Idempotency** | `Architecture` | Atomic Transactional Outbox dual-write, Idempotency key protection, and Clean Architecture layer boundary invariants. |

---

## 🏛️ Invariants & Rules
- **100% Public API Fidelity**: Only authentic APIs discovered in the Core & Infrastructure packages are demonstrated.
- **Zero Fictional APIs**: No simulated methods or fake extensions.
- **Executable Reference**: Every level executes fully in-memory with exit code 0.
