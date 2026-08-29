# Level 00: Conceptual & Architectural Foundations

> **Level:** 00 | **Category:** Conceptual | **Executable Reference:** [`Level0_Conceptual.cs`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/samples/Showcase/Levels/Level0_Conceptual.cs)

---

## 1. What is `EricksonLopez.Transaction`?

`EricksonLopez.Transaction` is a high-performance, explicit, composable, and Native AOT-ready transaction boundary coordinator built natively on top of ADO.NET [`DbConnection`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/src/EricksonLopez.Transaction.Abstractions/IDbConnectionFactory.cs) and `DbTransaction`.

It addresses fundamental consistency and architectural challenges in modern .NET 10 Clean Architecture / DDD applications:
- **Eliminates leaky transaction boundaries** across Application and Infrastructure layers.
- **Provides automatic hierarchical Savepoints** for nested transaction scopes.
- **Integrates seamlessly with functional `Result<T>` monads** for automatic rollback upon business failure without requiring exception throwing.
- **Accurately distinguishes ambiguous commit failures** (network drops vs database aborts) via `TransactionCommitException.IsAmbiguous`.
- **Emits native OpenTelemetry metrics and distributed tracing activities**.
- **Guarantees 100% Native AOT and trimming compliance** with zero dynamic reflection.

---

## 2. Comparative Matrix: Traditional Approaches vs `EricksonLopez.Transaction`

| Capability | Raw `DbTransaction` | `System.Transactions.TransactionScope` | `EricksonLopez.Transaction` |
|---|---|---|---|
| **API Paradigm** | Low-level ADO.NET | Legacy Ambient DTC | Explicit Async Coordinator |
| **Nested Scope Handling** | Throws Exception | Escalates to 2PC / MSDTC | Automatic SQL Savepoints |
| **`Result<T>` Monad Integration** | Manual Inspection | No Awareness | Automatic Failure Rollback |
| **Async Flow & AOT Safety** | Native / Manual | Async Flow & Alloc Issues | 100% Native AOT & Trimmable |
| **Commit Ambiguity Detection** | Generic `DbException` | Generic `TransactionException` | Explicit `IsAmbiguous` Flag |
| **OpenTelemetry Telemetry** | None | Limited / DiagnosticSource | Built-in `ActivitySource` & `Meter` |
| **Multi-Dialect Error Analysis** | Manual SQLSTATE Matching | Manual SQLSTATE Matching | 6 Engine Error Classifiers |
| **In-Memory Test Doubles** | Complex Mocks | Not Supported | `FakeTransactionManager` |

---

## 3. Architectural Scope & Boundary Invariants

### What the Library Does
- Controls physical `DbTransaction` lifecycles (`BeginAsync`, `CommitAsync`, `RollbackAsync`, `DisposeAsync`).
- Manages nested execution scopes via SQL Savepoints (`SAVEPOINT`, `ROLLBACK TO`, `RELEASE`).
- Propagates ambient transaction contexts across asynchronous execution flows (`AsyncLocal`).
- Binds Dapper queries, commands, and cancellation tokens to active transactions.
- Integrates with `EricksonLopez.Result` for monadic failure rollbacks.
- Exports OpenTelemetry metrics and distributed tracing spans.

### What the Library Rejects (Anti-Patterns)
- ❌ **NOT an ORM:** Does not perform change tracking or dynamic SQL generation.
- ❌ **NOT an Entity Unit of Work:** Does not maintain aggregate identity maps.
- ❌ **NOT an Internal Retry Engine:** Retrying queries inside an active transaction is unsafe; resilience belongs in outer policies.
- ❌ **NOT a Distributed 2PC Coordinator:** Rejects MSDTC; favors Sagas, Transactional Outbox, and Eventual Consistency.
