# ADR-031: Pluggable Native Oracle Dialect Specification

## Status
Accepted

## Date
2026-09-14

## Context
In ADR-016 and ADR-030, pluggable database dialects were designed to encapsulate engine-specific SQL syntax for savepoints, transaction modes, and error classification. While dedicated dialect implementations were provided for PostgreSQL, SQL Server, MySQL, and SQLite, Oracle Database connections initially defaulted to `GenericSqlDialect`.

Under Oracle SQL syntax:
1. Issuing `RELEASE SAVEPOINT` is not supported and triggers database error `ORA-00900: invalid SQL statement`.
2. Enabling read-only transaction mode requires executing `SET TRANSACTION READ ONLY;` as the first statement in the transaction.

Defaulting Oracle connections to `GenericSqlDialect` caused savepoint releases to attempt `RELEASE SAVEPOINT` (relying on a silent exception swallow in fallback paths) and failed to enforce physical read-only mode in the database engine.

## Decision
We introduce `OracleDialect` implementing `IDatabaseDialect` and register it in `DialectResolver`:

1. **Engine Detection**: `CanHandle(DbConnection)` inspects connection type names, matching `Oracle.ManagedDataAccess.Client.`, `Oracle.`, or types containing `"Oracle"`.
2. **Savepoint Syntax**:
   - `GetSavepointCreationSql`: Returns `$"SAVEPOINT {savepointName};"`.
   - `GetSavepointRollbackSql`: Returns `$"ROLLBACK TO SAVEPOINT {savepointName};"`.
   - `GetSavepointReleaseSql`: Returns `null`. By contract, returning `null` instructs `Savepoint.DisposeAsync` to bypass release SQL execution, preventing `ORA-00900`.
3. **Read-Only Mode Propagation**: `ApplyReadOnlyModeAsync` executes `SET TRANSACTION READ ONLY;` on the open transaction handle when `TransactionOptions.ReadOnly = true`.
4. **Registration**: Registered in `DialectResolver.DefaultDialects` as part of standard multi-engine resolution.

## Consequences

### Positive
- Prevents database runtime errors (`ORA-00900`) during Oracle savepoint disposal.
- Enforces strict read-only transaction mode in Oracle Database engines.
- Achieves 100% dialect parity across all 6 supported relational engines.

### Negative
- None. `OracleDialect` is internal to `EricksonLopez.Transaction` and introduces zero external dependencies.
