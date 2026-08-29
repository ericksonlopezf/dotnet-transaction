# ADR-008: PostgreSQL and Npgsql Error Code Classification

## Status
Accepted

## Context
PostgreSQL exposes deterministic error codes via the standard SQLSTATE string in `Npgsql.PostgresException.SqlState`. Different SQLSTATE codes represent distinct recovery semantics:
- `40001` (Serialization Failure): Expected under `Serializable` isolation level when concurrent transactions conflict. Resolved by retrying the entire transaction block.
- `40P01` (Deadlock Detected): Deadlock detected by PostgreSQL lock manager. Resolved by retrying the entire transaction block.
- `25P02` (In Failed SQL Transaction): A statement inside the transaction failed; all subsequent commands are rejected by the server until `ROLLBACK`.
- `57014` (Query Canceled): Statement canceled by timeout or client cancellation.

Without standardized classification, applications write brittle string-matching logic against exception messages.

## Decision
We provide `PostgreSqlErrorClassifier` in `EricksonLopez.Transaction.PostgreSql` containing static inspection methods:
1. `IsSerializationFailure(Exception)`
2. `IsDeadlock(Exception)`
3. `IsInFailedTransaction(Exception)`
4. `IsTransient(Exception)`

These methods inspect both the root exception and recursively unwrap nested `InnerException` chains.

## Consequences
### Positive
- Robust, standardized classification for PostgreSQL error states.
- Clean integration with outer retry policies (`EricksonLopez.Resilience`).
- Decouples core transaction management from provider-specific Npgsql binaries.

### Negative
- Applications using PostgreSQL must reference `EricksonLopez.Transaction.PostgreSql`.
