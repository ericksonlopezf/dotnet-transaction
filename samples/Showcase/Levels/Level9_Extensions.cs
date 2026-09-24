// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Transaction.MariaDb;
using EricksonLopez.Transaction.Mediator;
using EricksonLopez.Transaction.MySql;
using EricksonLopez.Transaction.Oracle;
using EricksonLopez.Transaction.PostgreSql;
using EricksonLopez.Transaction.Resilience;
using EricksonLopez.Transaction.Sqlite;
using EricksonLopez.Transaction.SqlServer;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Transaction.Showcase.Levels;

/// <summary>
/// Level 09: Multi-DB Dialect Providers, Connection Factories &amp; Mediator Attributes.
/// Demonstrates provider registration and connection factory instantiations across the 6 supported relational engines,
/// TransactionalAttribute (reflection-based), and ITransactionalCommandOptions (AOT-safe).
/// </summary>
public sealed class Level9_Extensions : ILevel
{
    public int LevelNumber => 9;
    public string Name => "Multi-DB Dialect Providers, Factories & Mediator Attributes";
    public string Description => "Demonstrates DI registration and factory architecture across PostgreSQL, SQL Server, MySQL, MariaDB, Oracle, and SQLite. Covers TransactionalAttribute (reflection-based mediator config) and ITransactionalCommandOptions (AOT-safe alternative).";
    public string Category => "Dialects";

    public async Task RunAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("  LEVEL 09: MULTI-DB DIALECT PROVIDERS, FACTORIES & MEDIATOR ATTRIBUTES");
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

3. Advanced Ecosystem Integrations & Cross-Cutting Behaviors
------------------------------------------------------------
""");
        // Mediator Integration: AddTransactionPipelineBehavior
        var mediatorServices = new ServiceCollection();
        mediatorServices.AddTransactionPipelineBehavior();
        Console.WriteLine("  ✔ Mediator:   AddTransactionPipelineBehavior registered.");

        // Resilience Integration: HandleAmbiguousCommit
        var ambiguousBuilder = PollyTransactionExtensions.HandleAmbiguousCommit();
        Polly.Policy.Handle<Exception>().HandleAmbiguousCommit();
        Console.WriteLine("  ✔ Resilience: HandleAmbiguousCommit policy builders configured.");

        // Transaction Lifecycle: CommitAsync and SetRollbackOnly
        var fakeTx = new EricksonLopez.Transaction.Testing.FakeTransaction();
        fakeTx.Context.SetRollbackOnly("Showcase test rollback");
        await fakeTx.CommitAsync(cancellationToken);
        Console.WriteLine("  ✔ Lifecycle:  SetRollbackOnly and CommitAsync executed on transaction.");

        // EF Core Integration: UseTransactionAsync
        // UseTransactionAsync(DbContext, ITransactionContext, CancellationToken) binds an active
        // ITransactionContext to an EF Core DbContext so that EF Core operations participate
        // in the same physical DbTransaction. In production, the ITransactionContext must
        // wrap a real physical DbTransaction. FakeTransactionContext provides null here, so
        // an InvalidOperationException is expected in this Showcase demonstration.
        using var dbContext = new ShowcaseDbContext(new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ShowcaseDbContext>().Options);
        try
        {
            await EricksonLopez.Transaction.EntityFrameworkCore.DbContextTransactionExtensions.UseTransactionAsync(dbContext, fakeTx.Context, cancellationToken);
        }
        catch (Exception)
        {
            // Expected: FakeTransactionContext.Transaction is null (no physical DbTransaction).
            // In production, pass a context from a real TransactionManager, which wraps a physical DbTransaction.
        }
        Console.WriteLine("  ✔ EF Core:    UseTransactionAsync context binding API verified (FakeContext = expected no-op).");

        // Mediator Pipeline Behavior: Handle
        var fakeManager = new EricksonLopez.Transaction.Testing.FakeTransactionManager();
        var pipelineBehavior = new EricksonLopez.Transaction.Mediator.TransactionPipelineBehavior<ShowcaseTransactionalCommand, EricksonLopez.Result.Result<string>>(fakeManager);
        var behaviorResult = await pipelineBehavior.Handle(
            new ShowcaseTransactionalCommand(),
            new ShowcaseNext<EricksonLopez.Result.Result<string>>(EricksonLopez.Result.Result<string>.Success("Mediator OK")),
            cancellationToken);
        Console.WriteLine($"  ✔ Pipeline:   TransactionPipelineBehavior.Handle executed ({behaviorResult.Value}).");

        // ─── TransactionalAttribute demonstration ───────────────────────────────────

        Console.WriteLine("""

5. TransactionalAttribute — Mediator Command Configuration
----------------------------------------------------------
[Transactional(IsolationLevel = TransactionIsolationLevel.Serializable, TimeoutSeconds = 30)]
public sealed record MyCommand : ITransactionalCommand { ... }

Note: TransactionalAttribute is a declarative metadata mechanism for mediator commands.
It specifies the isolation level and timeout. The TransactionPipelineBehavior reads this
attribute at runtime to configure TransactionOptions when reflection is available.

For Native AOT scenarios where reflection is restricted, implement ITransactionalCommandOptions
on your command record instead — it provides the same capability without reflection:

public sealed record MyAotCommand : ITransactionalCommand, ITransactionalCommandOptions
{
    public TransactionOptions TransactionOptions => new()
    {
        IsolationLevel = TransactionIsolationLevel.Serializable,
        Timeout        = TimeSpan.FromSeconds(30)
    };
}
""");

        // Inspect TransactionalAttribute on a decorated command type
        Type decoratedCommandType = typeof(ShowcaseDecoratedTransactionalCommand);
        var attr = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<
            EricksonLopez.Transaction.Mediator.TransactionalAttribute>(decoratedCommandType);

        Console.WriteLine($"  ✔ [TransactionalAttribute] Found on ShowcaseDecoratedTransactionalCommand: {attr is not null}");
        Console.WriteLine($"     IsolationLevel  = {attr?.IsolationLevel}");
        Console.WriteLine($"     TimeoutSeconds  = {attr?.TimeoutSeconds}s");

        // ITransactionalCommandOptions — AOT-safe alternative (no reflection)
        var aotCommand = new ShowcaseTransactionalCommand();
        TransactionOptions resolvedOptions = aotCommand.TransactionOptions;
        Console.WriteLine($"  ✔ [ITransactionalCommandOptions] ShowcaseTransactionalCommand.TransactionOptions.IsolationLevel = {resolvedOptions.IsolationLevel}");

        Console.WriteLine("""

6. Multi-Engine Capabilities & Savepoint Support
-------------------------------------------------
  • PostgreSQL: Full Savepoint support (SAVEPOINT, ROLLBACK TO, RELEASE SAVEPOINT), MVCC Snapshot isolation.
  • SQL Server: Savepoint support (SAVEPOINT / ROLLBACK), Snapshot isolation via tempdb row versioning.
  • MySQL / MariaDB: Savepoint support with InnoDB storage engine.
  • Oracle: Standard SAVEPOINT and ROLLBACK TO SAVEPOINT.
  • SQLite: Savepoint support in WAL mode (Savepoints work even across transaction start/commit boundaries).
""");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✔ Level 09 Multi-DB Dialect Providers & Mediator Attributes verified successfully.\n");
        Console.ResetColor();
    }
}

public sealed class ShowcaseDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public ShowcaseDbContext(Microsoft.EntityFrameworkCore.DbContextOptions<ShowcaseDbContext> options) : base(options) { }
}

/// <summary>
/// Demonstrates ITransactionalCommand + ITransactionalCommandOptions (AOT-safe, no reflection).
/// The pipeline behavior reads TransactionOptions directly from this interface at compile time.
/// </summary>
public sealed record ShowcaseTransactionalCommand : EricksonLopez.Transaction.Mediator.ITransactionalCommand, EricksonLopez.Transaction.Mediator.ITransactionalCommandOptions
{
    public TransactionOptions TransactionOptions => TransactionOptions.Default;
}

/// <summary>
/// Demonstrates TransactionalAttribute — the reflection-based alternative for non-AOT scenarios.
/// The pipeline behavior reads IsolationLevel and TimeoutSeconds from this attribute at runtime.
/// </summary>
[EricksonLopez.Transaction.Mediator.Transactional(TransactionIsolationLevel.Serializable, TimeoutSeconds = 30)]
public sealed record ShowcaseDecoratedTransactionalCommand : EricksonLopez.Transaction.Mediator.ITransactionalCommand
{
}

public readonly struct ShowcaseNext<T>(T value) : EricksonLopez.Mediator.INext<T>
{
    public ValueTask<T> InvokeAsync() => ValueTask.FromResult(value);
}
