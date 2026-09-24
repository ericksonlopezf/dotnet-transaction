# ADR-030: Pluggable Database Dialect Abstraction (IDatabaseDialect)

## Status
Accepted

## Date
2026-09-14

## Context
Relational database engines exhibit substantial syntax differences for transactional savepoints and session modes:
- **PostgreSQL**: Supports `SAVEPOINT name`, `ROLLBACK TO SAVEPOINT name`, `RELEASE SAVEPOINT name`, and `SET TRANSACTION READ ONLY`.
- **SQL Server**: Uses `SAVE TRANSACTION name` and `ROLLBACK TRANSACTION name`, without an explicit release command (savepoints are automatically released on commit).
- **MySQL / MariaDB**: Supports InnoDB savepoints (`SAVEPOINT name`, `ROLLBACK TO SAVEPOINT name`, `RELEASE SAVEPOINT name`) and session read-only directives.
- **Oracle**: Supports `SAVEPOINT name` and `ROLLBACK TO SAVEPOINT name`, with no explicit release syntax.
- **SQLite**: In WAL mode, supports `SAVEPOINT name`, `ROLLBACK TO name`, and `RELEASE name`.

Hardcoding these SQL dialect variations inside the core `TransactionManager` or `Savepoint` scope classes couples the core engine to specific SQL syntax and prevents consumers from extending the framework to custom or untested database drivers.

## Decision
We introduce the `IDatabaseDialect` abstraction in `EricksonLopez.Transaction.Abstractions` and a pluggable `DialectResolver` in `EricksonLopez.Transaction`:

1. **Dialect Interface**:
   ```csharp
   public interface IDatabaseDialect
   {
       bool CanHandle(DbConnection connection);
       Task ApplyReadOnlyModeAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default);
       string GetSavepointCreationSql(string savepointName);
       string GetSavepointRollbackSql(string savepointName);
       string? GetSavepointReleaseSql(string savepointName);
   }
   ```
2. **Built-in Dialects**: The core package provides built-in implementations:
   - `PostgreSqlDialect`
   - `SqlServerDialect`
   - `MySqlDialect`
   - `SqliteDialect`
   - `GenericSqlDialect` (fallback)
3. **Pluggable Registration**: `TransactionManager` accepts an optional `IEnumerable<IDatabaseDialect>?` collection in its constructor. When custom dialects are registered in the DI container, they take precedence over default resolvers.

## Consequences

### Positive
- Strict Open/Closed Principle: new relational engines can be supported without modifying the core state machine or savepoint logic.
- Consistent savepoint and read-only behavior across all 6 supported relational engines.
- Clean separation between ADO.NET coordination and SQL command text generation.

### Negative
- Adds an additional layer of abstraction between savepoint management and physical command execution.
