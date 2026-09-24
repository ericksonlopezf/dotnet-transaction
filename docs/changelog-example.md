# Changelog & Release Engineering Guide

> **Semantic Versioning, release classification standards, and example release notes for the EricksonLopez.Transaction ecosystem.**

---

## 1. Versioning Policy

All packages in the `EricksonLopez.Transaction` ecosystem strictly follow [Semantic Versioning 2.0.0](https://semver.org/):

```text
MAJOR.MINOR.PATCH
  │     │     │
  │     │     └── Bug fixes, internal performance tweaks, zero breaking changes
  │     └──────── New features, new dialect adapters, backwards-compatible additions
  └────────────── Breaking API changes, signature removals, major behavioral alterations
```

---

## 2. Release Classification Categories

Each release note is organized into standard Keep a Changelog categories:
- **`Added`**: New public types, methods, extension methods, or dialect providers.
- **`Changed`**: Changes in existing functionality or dependency version updates.
- **`Deprecated`**: Soon-to-be removed public API members.
- **`Removed`**: Features removed in a major release.
- **`Fixed`**: Bug fixes, race condition patches, or connection leak resolutions.
- **`Security`**: Security advisories, vulnerability patches, or metadata sanitization improvements.

---

## 3. Example Release Notes: Version 2.0.0

```markdown
## [2.0.0] - 2026-09-23

### Added
- **EricksonLopez.Transaction.EntityFrameworkCore**:
  - `DbContextTransactionExtensions.UseTransactionAsync`: Seamlessly enlists `DbContext` in ambient `ITransactionContext`.
  - Shared connection and transaction coordination between EF Core change tracker and raw Dapper queries.
- **EricksonLopez.Transaction.Mediator**:
  - `TransactionPipelineBehavior<TRequest, TResponse>`: Declarative command pipeline behavior.
  - `ITransactionalCommandOptions` for declarative runtime options and `[Transactional]` metadata marker.
- **EricksonLopez.Transaction.Resilience**:
  - `TransactionResilienceExtensions.ExecuteWithResilienceAsync`: Automatic retry and backoff around the outer transaction boundary.
- **EricksonLopez.Transaction.Analyzers**:
  - Roslyn Analyzer `ELT001`: Detects and warns about uncommitted or undisposed transaction handles instantiated via `BeginAsync`.
- **Abstractions**:
  - `TransactionPostCommitException`: Dedicated exception thrown when a physical database commit succeeds, but an `AfterCommitAsync` enlistment callback fails.
  - `IDatabaseDialect`: Interface abstracting database-specific savepoint syntax and error classifiers.
  - `TransactionOptions.SanitizeTelemetryMetadata`: When enabled, redacts `transaction.id` and `transaction.name` tags from OpenTelemetry distributed tracing spans.

### Changed
- Refactored `TransactionManager` to support pluggable `IDatabaseDialect` providers.
- Upgraded target frameworks to include `.NET 10.0` across all 15 packages.
- Enhanced OpenTelemetry activity tags with sanitized transaction names and isolation levels.

### Fixed
- Fixed potential socket disconnect ambiguity handling by marking `TransactionCommitException.IsAmbiguous = true` during transport dropouts.
- Corrected savepoint identifier escaping in SQLite dialect under WAL mode.

### Security
- Added automated telemetry metadata sanitization to prevent sensitive payload leaks in distributed tracing collectors.
```
