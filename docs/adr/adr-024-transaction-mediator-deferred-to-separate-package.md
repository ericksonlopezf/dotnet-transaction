# ADR-024: EricksonLopez.Transaction.Mediator — First-Class Ecosystem Package

## Status
Accepted (Implemented In-Repo as First-Class Package)

## Date
2026-09-04

## Context
In Clean Architecture / CQRS applications, command and query handlers executed via a mediator pipeline benefit from automatic transaction boundary management: open a transaction before the handler, commit on success, rollback on failure.

Providing this integration between `EricksonLopez.Transaction` and `EricksonLopez.Mediator` as a dedicated pipeline behavior strengthens the ecosystem moat and reduces boilerplate across all handler types in adopter applications.

## Decision
We implement `EricksonLopez.Transaction.Mediator` as a dedicated modular package directly within this repository:

- **Package Name**: `EricksonLopez.Transaction.Mediator`
- **Primary Artifact**: `TransactionPipelineBehavior<TRequest, TResponse>` implementing `IPipelineBehavior<TRequest, TResponse>` that:
  1. Extracts `TransactionOptions` from the incoming request (via `ITransactionalCommandOptions`).
  2. Calls `ITransactionManager.BeginAsync(...)` wrapping the inner pipeline execution.
  3. Enlists optional `ITransactionEnlistment` hooks when not suppressed.
  4. Commits on successful response; rolls back on exception or `Result.Failure`.

### Architectural Scope & Design Rationale
1. **Coupling Segregation**: `EricksonLopez.Transaction.Mediator` depends on `EricksonLopez.Transaction.Abstractions`, `EricksonLopez.Mediator`, and `EricksonLopez.Result`. The core transaction coordinator engine remains 100% free of mediator coupling.
2. **Native AOT Compliance**: To avoid reflection in AOT environments, options are resolved via `ITransactionalCommandOptions` without runtime type analysis.
3. **Automatic Functional Failure Rollback**: When a handler returns an `IResultOutcome` where `IsFailure` is true, the behavior automatically triggers `RollbackAsync()`.

## Consequences

### Positive
- Core transaction library remains free of mediator coupling.
- Turnkey transaction management experience for all CQRS command handlers.
- Ecosystem moat strengthens: adopting both libraries creates a deeply integrated, hard-to-replace solution.

### Negative
- Requires adopters using CQRS to reference the additional package `EricksonLopez.Transaction.Mediator`.
