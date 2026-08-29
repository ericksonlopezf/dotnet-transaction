# ADR-024: EricksonLopez.Transaction.Mediator — Deferred to Separate Package

## Status
Accepted (Deferred — Out of Scope for This Repository)

## Context
In Clean Architecture / CQRS applications, command and query handlers executed via a mediator pipeline benefit from automatic transaction boundary management: open a transaction before the handler, commit on success, rollback on failure.

Providing this integration between `EricksonLopez.Transaction` and `EricksonLopez.Mediator` as a dedicated pipeline behavior would strengthen the ecosystem moat and reduce boilerplate across all handler types in adopter applications.

## Decision
We **defer** the implementation of `EricksonLopez.Transaction.Mediator` to a **separate NuGet package** within the EricksonLopez ecosystem:

- **Package Name**: `EricksonLopez.Transaction.Mediator`
- **Primary Artifact**: A `TransactionBehavior<TRequest, TResponse>` implementing `IPipelineBehavior<TRequest, TResponse>` that:
  1. Extracts `TransactionOptions` from the incoming request (via optional `ITransactional` marker interface or attribute).
  2. Calls `ITransactionManager.ExecuteAsync(...)` or `ExecuteResultAsync(...)` wrapping the inner pipeline execution.
  3. Commits on successful response; rolls back on exception or `Result.Failure`.

### Deferral Rationale
1. **Dependency on EricksonLopez.Mediator stability**: The pipeline behavior contract in `EricksonLopez.Mediator` must be in a stable, publicly released version before this integration can be authored and validated.
2. **Independent versioning required**: The Mediator integration package must version independently from the core transaction library to avoid forcing upgrades across the ecosystem.
3. **Scope boundary**: This repository (`dotnet-transaction`) is the single-responsibility owner of the transaction coordination contract. Mediator integration belongs in a composition layer above both libraries.

### Prerequisites
- `EricksonLopez.Mediator` >= stable release with a finalized `IPipelineBehavior<,>` contract.
- `EricksonLopez.Transaction` >= 1.1.0 with parameterless overloads and `ExecuteResultAsync`.

## Consequences

### Positive
- Core transaction library remains free of mediator coupling.
- When the integration package ships, it provides a turnkey transaction management experience for all CQRS command handlers.
- Ecosystem moat strengthens: adopting both libraries creates a deeply integrated, hard-to-replace solution.

### Negative
- Until the package ships, teams must manually wrap handlers in `ExecuteAsync(...)` or `ExecuteResultAsync(...)`.
