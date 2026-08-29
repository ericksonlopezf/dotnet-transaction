# Product Strategy: EricksonLopez.Transaction
## From Feature Matrix to Investment Decisions

> **Date**: 2026-08-27  
> **Analyzed Version**: 1.0.0 (with 1.1.0 enhancements)  
> **Base Input**: `functional-parity-audit.md`  
> **Role**: Senior Product Strategist + Competitive Intelligence Analyst  

---

## 1. Product Context & Problem Statement

In enterprise .NET applications with Clean Architecture and DDD utilizing Dapper or direct ADO.NET, cross-layer transactional boundary coordination is consistently problematic:

- **Uncoordinated Propagation**: Duplicate `DbConnection` instances and uncoordinated `IDbTransaction` references across repositories.
- **Leaky Ownership**: The application layer loses control over when commits or rollbacks take place.
- **Flawed Retry Patterns**: Outer resilience wrapping single queries inside aborted transaction blocks (e.g. PostgreSQL `SQLSTATE 25P02`).
- **Commit Ambiguity**: Network disconnection during physical commit leads to generic exceptions and unknown database state.
- **Leaky Result Patterns**: `Result<T>.Failure` returning without automatic rollback leads to persisted inconsistent state.

### Target Audience

- **Primary**: Senior .NET Developers and Technical Leads implementing Clean Architecture / DDD with Dapper, multiple atomic repositories, OpenTelemetry, and Native AOT.
- **Secondary**: Engineering teams migrating from legacy `System.Transactions.TransactionScope` to modern async .NET.

---

## 2. Feature Matrix Audit

### Key Classification of Capabilities

#### A. Competitive Parity (Baseline Market Expectations)
- Asynchronous Begin / Commit / Rollback (`ITransactionManager`, `ITransaction`).
- Automatic rollback on uncommitted disposal (`IAsyncDisposable`).
- Configurable relational isolation levels (`TransactionIsolationLevel`).
- Microsoft Dependency Injection integration (`AddTransaction`, provider extensions).
- Fluent Dapper command binding (`context.AsCommand(...)`, `ExecuteAsync`, `QueryAsync`).
- Structured exception hierarchy (`TransactionException`, `TransactionCommitException.IsAmbiguous`).
- Ambient async-safe context propagation (`AsyncLocal<ITransactionContext?>`).

#### B. Architectural Strengths & Differentiators
- **100% Asynchronous Lifecycle**: End-to-end async commit, rollback, and disposal.
- **First-Class Savepoints**: Automatic relational savepoint scoping on nested execution (`NestedTransactionBehavior.UseSavepoint`).
- **`ITransactionEnlistment` Hooks**: Participant lifecycle hooks enabling Outbox and Idempotency dual-write patterns without coupling.
- **Native AOT & Trimming Certified**: Enforced via `<EnableTrimAnalyzer>true` with zero dynamic reflection.
- **Turn-Key In-Box Test Doubles**: `FakeTransactionManager` and `FakeTransaction` enabling pure in-memory unit testing.
- **Native OpenTelemetry Instrumentation**: Pre-configured `ActivitySource` and `Meter` metrics out-of-the-box.
- **Multi-Dialect Error Classifiers**: Tailored error predicates across 6 database dialects.

---

## 3. Opportunity Scoring & Prioritization

| Opportunity | User Impact | Market Demand | Competitive Pressure | Differentiation | Adoption Potential | Effort | Risk | **Score** |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| Fix `Suppress` Behavior | **5** | 4 | 5 | 2 | 5 | 5 | 5 | **31** |
| Fix `ReadOnly` Propagation | **4** | 3 | 4 | 3 | 4 | 4 | 4 | **26** |
| `Transaction.Mediator` Package | **4** | 4 | 2 | 5 | 5 | 2 | 3 | **25** |
| Dapper `QueryMultipleAsync` | **3** | 3 | 4 | 1 | 3 | 5 | 5 | **24** |
| `ITransactionEnlistment.OnExceptionAsync` | **4** | 3 | 1 | 5 | 4 | 3 | 4 | **24** |
| `TransactionDiagnostics` Test Suite | **4** | 2 | 3 | 1 | 3 | 5 | 5 | **23** |
| `Transaction.AspNetCore` Package | **3** | 3 | 2 | 4 | 4 | 2 | 3 | **21** |
| Dapper `ExecuteReaderAsync` | **2** | 2 | 3 | 1 | 2 | 5 | 5 | **20** |

---

## 4. Product Roadmap

### NOW (v1.0.1) — Correctness Sprint
- ✅ **Suppressed Scope (`NestedTransactionBehavior.Suppress`)**: Suspend ambient context, execute non-transactionally, and restore upon disposal.
- ✅ **ReadOnly Transaction Propagation**: Issue `SET TRANSACTION READ ONLY` on PostgreSQL and supported drivers when `TransactionOptions.ReadOnly` is enabled.
- ✅ **Diagnostics Test Coverage**: Test suite covering `ActivitySource` and `Meter` instruments.
- ✅ **Document `Task.WhenAll` Hazards**: Document single-connection async-flow invariants ([ADR-021](docs/adr/adr-021-asynchronous-execution-safety-and-task-whenall-invariants.md)).

### NEXT (v1.1.0) — Parity & Hardening
- ✅ **Dapper Multi-Result Extensions**: `QueryMultipleAsync` and `ExecuteReaderAsync` on `ITransactionContext`.
- ✅ **Parameterless Overloads**: `ExecuteAsync(Func<Task>)` and `ExecuteResultAsync(Func<Task<Result>>)`.
- ✅ **Structured Logging**: Zero-allocation `[LoggerMessage]` source generator in `TransactionManager` ([ADR-026](docs/adr/adr-026-ilogger-structured-logging-activation.md)).
- ✅ **Test Hardening**: Timeout expiration, hook failures, and commit ambiguity isolation tests.

### LATER (v2.0.0) — Ecosystem Moat
- ✅ **`ITransactionEnlistment.OnExceptionAsync`**: Hook invoked upon commit/rollback failure with exception suppression ([ADR-019](docs/adr/adr-019-lifecycle-exception-hook-enlistment.md)).
- ⏳ **`EricksonLopez.Transaction.Mediator`**: Turn-key transactional pipeline behavior ([ADR-024](docs/adr/adr-024-transaction-mediator-deferred-to-separate-package.md)).
- ⏳ **`EricksonLopez.Transaction.AspNetCore`**: Exploratory per-request middleware ([ADR-025](docs/adr/adr-025-transaction-aspnetcore-exploratory-deferral.md)).

---

## 5. What We Will NOT Build (Systematic Rejections)

1. **Distributed 2PC / MSDTC**: Rejected in [ADR-012](docs/adr/adr-012-rejection-of-distributed-2pc-transactions.md) in favor of Transactional Outbox, Sagas, and Idempotent Consumers.
2. **Internal Retry Engine**: Rejected in [ADR-011](docs/adr/adr-011-rejection-of-internal-retry-engine.md) (retries inside aborted PostgreSQL transactions fail with `25P02`; resilience must wrap `BeginAsync`).
3. **ORM Change Tracking & Query Caching**: Rejected in [ADR-023](docs/adr/adr-023-rejection-of-infrastructure-scope-expansion.md) to keep `EricksonLopez.Transaction` focused strictly on the transaction boundary.
4. **Ergonomic Feature Creep**: Fluent builders, sync Dapper overloads, and health checks rejected in [ADR-022](docs/adr/adr-022-rejection-of-ergonomic-feature-proposals.md).

---

## 6. Strategic Differentiation Pillars

1. **Reliability**: Deterministic lock-free state transitions, commit ambiguity detection (`IsAmbiguous`), and async-first auto-rollback.
2. **Composability**: Decoupled lifecycle enlistments (`ITransactionEnlistment`), hierarchical SQL savepoints, and monadic Result integration.
3. **Modern .NET**: Native AOT certified (`EnableTrimAnalyzer=true`), OpenTelemetry native, and high-performance zero-allocation design.
