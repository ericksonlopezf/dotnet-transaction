# Level 09: Multi-DB Dialect Providers, Factories & Mediator Attributes

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

| Engine | Savepoints | Read-Only Mode | Snapshot Isolation | Error Classifier |
|---|---|---|---|---|
| PostgreSQL | ✅ Full (`SAVEPOINT`/`ROLLBACK TO`/`RELEASE`) | ✅ `SET TRANSACTION READ ONLY` | ✅ MVCC | `PostgreSqlErrorClassifier` |
| SQL Server | ✅ (`SAVE TRANSACTION`/`ROLLBACK`) | ❌ | ✅ tempdb row versioning | `SqlServerErrorClassifier` |
| MySQL/MariaDB | ✅ InnoDB | ❌ | ❌ | `MySqlErrorClassifier` |
| Oracle | ✅ (`SAVEPOINT`/`ROLLBACK TO SAVEPOINT`) | ❌ | ❌ | `OracleErrorClassifier` |
| SQLite | ✅ WAL mode | ❌ | ❌ | `SqliteErrorClassifier` |

---

## 4. `TransactionalAttribute` — Reflection-Based Command Configuration

`TransactionalAttribute` decorates a mediator command class to specify transaction isolation level and timeout declaratively. The `TransactionPipelineBehavior` reads this attribute at runtime.

```csharp
[Transactional(TransactionIsolationLevel.Serializable, TimeoutSeconds = 30)]
public sealed record PlaceOrderCommand : ITransactionalCommand
{
    public Guid OrderId { get; init; }
    public decimal Amount { get; init; }
}
```

> **AOT Note:** `TransactionalAttribute` uses reflection (`CustomAttributeExtensions.GetCustomAttribute<T>`). In Native AOT scenarios where reflection is restricted, implement `ITransactionalCommandOptions` on your command record instead.

---

## 5. `ITransactionalCommandOptions` — AOT-Safe Alternative (No Reflection)

```csharp
public sealed record PlaceOrderCommand : ITransactionalCommand, ITransactionalCommandOptions
{
    public Guid OrderId { get; init; }
    public decimal Amount { get; init; }

    // TransactionPipelineBehavior reads this at compile-time — no reflection
    public TransactionOptions TransactionOptions => new()
    {
        IsolationLevel = TransactionIsolationLevel.Serializable,
        Timeout = TimeSpan.FromSeconds(30)
    };
}
```

**Comparison:**

| Approach | AOT-Safe | Reflection-Free | Per-Instance Options |
|---|---|---|---|
| `TransactionalAttribute` | ❌ | ❌ | ❌ (class-level) |
| `ITransactionalCommandOptions` | ✅ | ✅ | ✅ (instance-level) |

---

## 6. `TransactionPipelineBehavior` Registration

```csharp
services.AddTransactionPipelineBehavior();
// Registers: services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionPipelineBehavior<,>))
// Only activates for commands implementing ITransactionalCommand.
```

The behavior:
1. Calls `ITransactionManager.BeginAsync()` with options from `ITransactionalCommandOptions` (or null for defaults)
2. Executes enlistments on the transaction context
3. Invokes the handler via `next.InvokeAsync()`
4. If the response implements `IResultOutcome` and `IsFailure`, calls `RollbackAsync`
5. Otherwise calls `CommitAsync`
6. On unhandled exceptions, calls `RollbackAsync` and re-throws
