# Architecture Decision Records (ADRs) Catalog

> **Copyright © Erickson Lopez. MIT License.**
> **Author:** Erickson Lopez ([ericksonlopezf@gmail.com](mailto:ericksonlopezf@gmail.com))
> **Repository:** [github.com/ericksonlopezf/dotnet-transaction](https://github.com/ericksonlopezf/dotnet-transaction)

---

## Master ADR Index

This document catalogs the architectural decisions governing the design, scope, non-functional requirements, and rejections for `EricksonLopez.Transaction`.

| ADR | Title | Status | Scope / Impact Area |
|---|---|---|---|
| [**ADR-001**](../adr/adr-001-transaction-boundary-ownership.md) | Transaction Boundary Ownership | **Accepted** | Application Layer coordinates boundaries; Repositories execute SQL. |
| [**ADR-002**](../adr/adr-002-package-segregation-topology.md) | Package Segregation Topology | **Accepted** | Pure BCL Abstractions segregated from Core Engine and Dialect packages. |
| [**ADR-003**](../adr/adr-003-nested-transaction-savepoint-semantics.md) | Nested Transaction Savepoint, Participation & Suppression Semantics | **Accepted** | 4 deterministic nested scopes: Savepoint, JoinExisting, RequireNew, Suppress. |
| [**ADR-004**](../adr/adr-004-commit-ambiguity-and-uncertain-state.md) | Commit Ambiguity and Uncertain State | **Accepted** | Explicit `TransactionCommitException.IsAmbiguous` on network drops. |
| [**ADR-005**](../adr/adr-005-resilience-and-retry-boundary.md) | Resilience & Retry Boundary Delegation | **Accepted** | Outer resilience policies wrap the entire transaction from BeginAsync. |
| [**ADR-006**](../adr/adr-006-result-monad-failure-rollback-integration.md) | Result Monad Failure Rollback Integration | **Accepted** | `ExecuteResultAsync` automatically triggers rollback on `Result.Failure`. |
| [**ADR-007**](../adr/adr-007-ambient-async-local-context-flow.md) | Ambient AsyncLocal Context Flow | **Accepted** | Execution context flows down async call tree via `AsyncLocal`. |
| [**ADR-008**](../adr/adr-008-postgresql-npgsql-error-classification.md) | PostgreSQL Npgsql Error Classification | **Accepted** | Diagnostics for SQLSTATE `40001`, `40P01`, `25P02`. |
| [**ADR-009**](../adr/adr-009-dapper-command-definition-binding.md) | Dapper CommandDefinition Binding | **Accepted** | Explicit `CommandDefinition` structs bound to active `DbTransaction`. |
| [**ADR-010**](../adr/adr-010-native-aot-and-trimming-invariants.md) | Native AOT & Trimming Invariants | **Accepted** | Zero unannotated reflection; verified with `AotSmokeTest`. |
| [**ADR-011**](../adr/adr-011-rejection-of-internal-retry-engine.md) | Rejection of Internal Retry Engine | **Accepted (Rejection)** | Retrying inside aborted transactions semantically incorrect; violates SRP. |
| [**ADR-012**](../adr/adr-012-rejection-of-distributed-2pc-transactions.md) | Rejection of Distributed 2PC Transactions | **Accepted (Rejection)** | MSDTC / 2PC rejected in favor of Sagas, Outbox, and Idempotency. |
| [**ADR-013**](../adr/adr-013-unit-of-work-coexistence-model.md) | Unit of Work Coexistence Model | **Accepted** | Transaction coordinator coexists orthogonally with Unit of Work / Repositories. |
| [**ADR-014**](../adr/adr-014-outbox-and-idempotency-dual-write-atomicity.md) | Outbox & Idempotency Dual-Write Atomicity | **Accepted** | Business entity and Outbox record persisted in same transaction. |
| [**ADR-015**](../adr/adr-015-opentelemetry-metrics-and-tracing.md) | OpenTelemetry Metrics and Tracing | **Accepted** | Native `ActivitySource` and `Meter` under `"EricksonLopez.Transaction"`. |
| [**ADR-016**](../adr/adr-016-multi-dialect-provider-topology-and-error-classifiers.md) | Multi-Dialect Provider Topology | **Accepted** | 6 dedicated packages for PostgreSQL, SQL Server, MySQL, MariaDB, Oracle, SQLite. |
| [**ADR-017**](../adr/adr-017-test-symmetry-and-architecture-rules.md) | Test Symmetry and Architecture Rules | **Accepted** | Automated boundary validation with NetArchTest. |
| [**ADR-018**](../adr/adr-018-read-only-transaction-mode-propagation.md) | ReadOnly Transaction Mode Propagation | **Accepted** | Database-enforced read-only mode (e.g. PostgreSQL `SET TRANSACTION READ ONLY`). |
| [**ADR-019**](../adr/adr-019-lifecycle-exception-hook-enlistment.md) | Lifecycle Exception Hook in Transaction Enlistment | **Accepted** | Non-breaking `OnExceptionAsync` hook with secondary exception suppression. |
| [**ADR-020**](../adr/adr-020-parameterless-execution-and-dapper-extensions.md) | Parameterless Execution & Dapper Multi-Result Extensions | **Accepted** | Clean syntax overloads and full Dapper `QueryMultipleAsync` / `ExecuteReaderAsync` support. |
| [**ADR-021**](../adr/adr-021-asynchronous-execution-safety-and-task-whenall-invariants.md) | Asynchronous Execution Safety & `Task.WhenAll` Invariants | **Accepted** | Single-threaded ADO.NET connection rules and ambient context concurrency safety. |
| [**ADR-022**](../adr/adr-022-rejection-of-ergonomic-feature-proposals.md) | Rejection of Ergonomic Feature Proposals | **Accepted (Rejection)** | Fluent Builder, Sync Dapper Overloads, Health Checks, TransactionScope Shim — all rejected. |
| [**ADR-023**](../adr/adr-023-rejection-of-infrastructure-scope-expansion.md) | Rejection of Infrastructure Scope Expansion | **Accepted (Rejection)** | ORM Change Tracking, Custom Connection Pooling, Query Caching, EF Core Integration — all rejected. |
| [**ADR-024**](../adr/adr-024-transaction-mediator-deferred-to-separate-package.md) | Transaction.Mediator — Deferred to Separate Package | **Accepted (Deferred)** | Mediator pipeline behavior deferred pending `EricksonLopez.Mediator` stable release. |
| [**ADR-025**](../adr/adr-025-transaction-aspnetcore-exploratory-deferral.md) | Transaction.AspNetCore — Exploratory Deferral | **Accepted (Deferred)** | Per-request middleware deferred pending demand validation and semantic resolution. |
| [**ADR-026**](../adr/adr-026-ilogger-structured-logging-activation.md) | ILogger Structured Logging Activation | **Accepted** | Optional zero-allocation `[LoggerMessage]` logging activated in `TransactionManager`. |
