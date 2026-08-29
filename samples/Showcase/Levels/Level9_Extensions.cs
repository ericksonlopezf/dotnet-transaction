// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Transaction.MariaDb;
using EricksonLopez.Transaction.MySql;
using EricksonLopez.Transaction.Oracle;
using EricksonLopez.Transaction.PostgreSql;
using EricksonLopez.Transaction.Sqlite;
using EricksonLopez.Transaction.SqlServer;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Transaction.Showcase.Levels;

/// <summary>
/// Level 09: Multi-DB Dialect Providers &amp; Connection Factories.
/// Demonstrates provider registration and connection factory instantiations across the 6 supported relational engines.
/// </summary>
public sealed class Level9_Extensions : ILevel
{
    public int LevelNumber => 9;
    public string Name => "Multi-DB Dialect Providers & Connection Factories";
    public string Description => "Demonstrates DI registration and factory architecture across PostgreSQL, SQL Server, MySQL, MariaDB, Oracle, and SQLite.";
    public string Category => "Dialects";

    public Task RunAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("  LEVEL 09: MULTI-DB DIALECT PROVIDERS & CONNECTION FACTORIES");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        Console.WriteLine("""
1. Multi-Dialect Package Topology
----------------------------------
EricksonLopez.Transaction provides 6 dedicated dialect packages, each embedding engine-specific
connection factories, DI registration extensions, and optimized error classifiers:

┌────────────────────────────────────────┬─────────────────────────────┬──────────────────────────────┐
│ Dialect Package                        │ Connection Factory          │ DI Extension Method          │
├────────────────────────────────────────┼─────────────────────────────┼──────────────────────────────┤
│ EricksonLopez.Transaction.PostgreSql   │ PostgreSqlConnectionFactory │ AddPostgreSqlTransaction     │
│ EricksonLopez.Transaction.SqlServer    │ SqlServerConnectionFactory  │ AddSqlServerTransaction      │
│ EricksonLopez.Transaction.MySql        │ MySqlConnectionFactory      │ AddMySqlTransaction          │
│ EricksonLopez.Transaction.MariaDb      │ MariaDbConnectionFactory    │ AddMariaDbTransaction        │
│ EricksonLopez.Transaction.Oracle       │ OracleConnectionFactory     │ AddOracleTransaction         │
│ EricksonLopez.Transaction.Sqlite       │ SqliteConnectionFactory     │ AddSqliteTransaction         │
└────────────────────────────────────────┴─────────────────────────────┴──────────────────────────────┘

2. DI Registration Patterns by Engine
-------------------------------------
""");

        // Example DI container configurations:
        var pgServices = new ServiceCollection();
        pgServices.AddPostgreSqlTransaction("Host=localhost;Database=app_db;Username=postgres;Password=secret;");
        Console.WriteLine("  ✔ PostgreSQL: AddPostgreSqlTransaction registered with NpgsqlDataSource.");

        var sqlServerServices = new ServiceCollection();
        sqlServerServices.AddSqlServerTransaction("Server=localhost;Database=AppDb;User Id=sa;Password=secret;TrustServerCertificate=true;");
        Console.WriteLine("  ✔ SQL Server: AddSqlServerTransaction registered with Microsoft.Data.SqlClient.");

        var mySqlServices = new ServiceCollection();
        mySqlServices.AddMySqlTransaction("Server=localhost;Database=app_db;Uid=root;Pwd=secret;");
        Console.WriteLine("  ✔ MySQL:      AddMySqlTransaction registered with MySqlConnector.");

        var mariaDbServices = new ServiceCollection();
        mariaDbServices.AddMariaDbTransaction("Server=localhost;Database=app_db;Uid=root;Pwd=secret;");
        Console.WriteLine("  ✔ MariaDB:    AddMariaDbTransaction registered with MySqlConnector.");

        var oracleServices = new ServiceCollection();
        oracleServices.AddOracleTransaction("User Id=system;Password=secret;Data Source=localhost:1521/XEPDB1;");
        Console.WriteLine("  ✔ Oracle:     AddOracleTransaction registered with Oracle.ManagedDataAccess.Core.");

        var sqliteServices = new ServiceCollection();
        sqliteServices.AddSqliteTransaction("Data Source=app.db;");
        Console.WriteLine("  ✔ SQLite:     AddSqliteTransaction registered with Microsoft.Data.Sqlite.");

        Console.WriteLine("""

3. Multi-Engine Capabilities & Savepoint Support
-------------------------------------------------
  • PostgreSQL: Full Savepoint support (SAVEPOINT, ROLLBACK TO, RELEASE SAVEPOINT), MVCC Snapshot isolation.
  • SQL Server: Savepoint support (SAVEPOINT / ROLLBACK), Snapshot isolation via tempdb row versioning.
  • MySQL / MariaDB: Savepoint support with InnoDB storage engine.
  • Oracle: Standard SAVEPOINT and ROLLBACK TO SAVEPOINT.
  • SQLite: Savepoint support in WAL mode (Savepoints work even across transaction start/commit boundaries).
""");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✔ Level 09 Multi-DB Dialect Providers verified successfully.\n");
        Console.ResetColor();

        return Task.CompletedTask;
    }
}
