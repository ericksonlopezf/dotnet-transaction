# ADR-029: Roslyn Diagnostic Analyzer for Direct Connection Manipulation (ELT001)

## Status
Accepted

## Date
2026-09-14

## Context
`ITransactionContext` exposes the underlying `DbConnection Connection { get; }` property so that repositories can execute SQL queries with micro-ORMs like Dapper or raw ADO.NET commands. 

However, because `DbConnection` has mutable lifecycle methods (`Close()`, `Dispose()`, `DisposeAsync()`, `ChangeDatabase()`, `BeginTransaction()`, `BeginTransactionAsync()`), a developer or library might inadvertently call these methods directly on `context.Connection`. Doing so closes or corrupts the physical connection while `TransactionManager` and its internal state machine still consider the transaction active, leading to difficult-to-diagnose runtime exceptions and leaked resources.

## Decision
We introduce `EricksonLopez.Transaction.Analyzers` providing compile-time Roslyn diagnostic analyzer `ELT001` (`ConnectionManipulationAnalyzer`):

1. **Rule Descriptor**:
   - **ID**: `ELT001`
   - **Title**: Direct manipulation of DbConnection in ITransactionContext
   - **Severity**: Error
   - **Category**: Usage
2. **Analysis Target**: Syntax invocations of `Close`, `Dispose`, `DisposeAsync`, `ChangeDatabase`, `BeginTransaction`, and `BeginTransactionAsync` where the receiver expression resolves semantically to the `Connection` property of `ITransactionContext`.
3. **Compile-Time Enforcement**: Any attempt to manipulate the physical connection lifecycle directly inside transactional code is flagged as a compilation error before runtime execution.

## Consequences

### Positive
- Enforces architectural invariants at compile time with zero runtime performance cost.
- Eliminates state machine corruption caused by manual connection closure.
- Protects developers from accidental transaction boundary errors.

### Negative
- Requires projects consuming the analyzer to target .NET SDKs supporting Roslyn components.
