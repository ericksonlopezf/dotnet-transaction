# Level 09: Multi-DB Dialect Providers & Connection Factories

> **Level:** 09 | **Category:** Dialects | **Executable Reference:** [`Level9_Extensions.cs`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/samples/Showcase/Levels/Level9_Extensions.cs)

---

## 1. Multi-Dialect Package Topology

`EricksonLopez.Transaction` provides 6 dedicated dialect packages, embedding engine-specific connection factories, DI registration extensions, and error classifiers:

| Dialect Package | Connection Factory | DI Extension Method | Engine Driver |
|---|---|---|---|
| `EricksonLopez.Transaction.PostgreSql` | `PostgreSqlConnectionFactory` | `AddPostgreSqlTransaction` | `Npgsql` |
| `EricksonLopez.Transaction.SqlServer` | `SqlServerConnectionFactory` | `AddSqlServerTransaction` | `Microsoft.Data.SqlClient` |
| `EricksonLopez.Transaction.MySql` | `MySqlConnectionFactory` | `AddMySqlTransaction` | `MySqlConnector` |
| `EricksonLopez.Transaction.MariaDb` | `MariaDbConnectionFactory` | `AddMariaDbTransaction` | `MySqlConnector` |
| `EricksonLopez.Transaction.Oracle` | `OracleConnectionFactory` | `AddOracleTransaction` | `Oracle.ManagedDataAccess.Core` |
| `EricksonLopez.Transaction.Sqlite` | `SqliteConnectionFactory` | `AddSqliteTransaction` | `Microsoft.Data.Sqlite` |

---

## 2. DI Setup by Database Engine

```csharp
// PostgreSQL
builder.Services.AddPostgreSqlTransaction("Host=localhost;Database=app_db;Username=postgres;Password=secret;");

// SQL Server
builder.Services.AddSqlServerTransaction("Server=localhost;Database=AppDb;User Id=sa;Password=secret;TrustServerCertificate=true;");

// MySQL
builder.Services.AddMySqlTransaction("Server=localhost;Database=app_db;Uid=root;Pwd=secret;");

// MariaDB
builder.Services.AddMariaDbTransaction("Server=localhost;Database=app_db;Uid=root;Pwd=secret;");

// Oracle
builder.Services.AddOracleTransaction("User Id=system;Password=secret;Data Source=localhost:1521/XEPDB1;");

// SQLite
builder.Services.AddSqliteTransaction("Data Source=app.db;");
```

---

## 3. Engine Capabilities & Savepoint Support Matrix

- **PostgreSQL**: Full Savepoint support (`SAVEPOINT`, `ROLLBACK TO`, `RELEASE SAVEPOINT`), MVCC Snapshot isolation.
- **SQL Server**: Savepoint support (`SAVEPOINT` / `ROLLBACK`), Snapshot isolation via tempdb row versioning.
- **MySQL / MariaDB**: Savepoint support with InnoDB storage engine.
- **Oracle**: Standard `SAVEPOINT` and `ROLLBACK TO SAVEPOINT`.
- **SQLite**: Savepoint support in WAL mode (Savepoints function cleanly across transaction boundaries).
