# ADR-022: Rejection of Ergonomic Feature Proposals

## Status
Accepted (Rejection)

## Context
During product strategy analysis and competitive feature matrix review, several ergonomic convenience features were proposed that might superficially improve developer experience but provide no architectural value and actively regress the library's design invariants.

## Decision
We **systematically reject** the following ergonomic proposals:

### 1. Fluent Builder for `TransactionOptions`
**Proposed**: `TransactionOptions.Builder().WithIsolationLevel(...).WithTimeout(...).Build()`

**Rejection Rationale**:
- C# 9+ `record` syntax with `with` expressions provides a superior alternative: `TransactionOptions.Default with { IsolationLevel = ..., Timeout = ... }`.
- A builder adds public API surface area, a new type, and compilation overhead without any behavioral value.
- Factory methods (`TransactionOptions.WithTimeout(...)`) cover the most common use cases idiomatically.

### 2. Synchronous Dapper Overloads
**Proposed**: `context.Execute(...)`, `context.Query<T>(...)`, etc.

**Rejection Rationale**:
- `EricksonLopez.Transaction` targets .NET 8+ async-first. Synchronous I/O is an explicit regression.
- No synchronous ADO.NET method is able to fully honor `CancellationToken` semantics.
- Mixing sync overloads invites thread pool starvation patterns in ASP.NET Core server applications.
- All adopters are expected to be on .NET 8+ with `async`/`await` throughout.

### 3. Health Checks Built-In
**Proposed**: Implementing `IHealthCheck` or connection heartbeat inside the library.

**Rejection Rationale**:
- Health checking is a cross-cutting concern owned by the hosting layer (`Microsoft.Extensions.Diagnostics.HealthChecks`).
- Database connection health is already handled by ADO.NET connection pooling and driver-level keep-alive mechanisms.
- Embedding health checks would couple the library to ASP.NET Core abstractions, violating the BCL-only principle of the core package.

### 4. `System.Transactions.TransactionScope` Compatibility Shim
**Proposed**: A shim that wraps `TransactionScope` or re-implements `ITransactionScope` semantics.

**Rejection Rationale**:
- `EricksonLopez.Transaction` is the modern alternative to `TransactionScope`, not a wrapper around it.
- `TransactionScope` relies on ambient `[ThreadStatic]` and MSDTC escalation, both incompatible with async workflows and Native AOT.
- A shim would inherit all the drawbacks of `TransactionScope` while providing no improvement. Developers migrating from `TransactionScope` should adopt the `ITransactionManager` pattern directly.

## Consequences

### Positive
- API surface remains minimal and purposeful.
- No regression in .NET 10 async-first ergonomics.
- No additional coupling to ASP.NET Core or `System.Transactions`.

### Negative
- Developers coming from blocking codebases must migrate to `async`/`await` patterns.
