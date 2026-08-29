// Copyright © Erickson Lopez. MIT License.
using System;
using System.Reflection;
using AwesomeAssertions;
using EricksonLopez.Transaction.Diagnostics;
using NetArchTest.Rules;
using Xunit;

namespace EricksonLopez.Transaction.ArchitectureTests;

public sealed class TransactionArchitectureTests
{
    private static readonly Assembly AbstractionsAssembly = typeof(ITransaction).Assembly;
    private static readonly Assembly CoreAssembly = typeof(TransactionManager).Assembly;
    private static readonly Assembly DapperAssembly = typeof(Dapper.TransactionDapperExtensions).Assembly;
    private static readonly Assembly PostgreSqlAssembly = typeof(PostgreSql.PostgreSqlConnectionFactory).Assembly;
    private static readonly Assembly SqlServerAssembly = typeof(SqlServer.SqlServerConnectionFactory).Assembly;
    private static readonly Assembly MySqlAssembly = typeof(MySql.MySqlConnectionFactory).Assembly;
    private static readonly Assembly MariaDbAssembly = typeof(MariaDb.MariaDbConnectionFactory).Assembly;
    private static readonly Assembly OracleAssembly = typeof(Oracle.OracleConnectionFactory).Assembly;
    private static readonly Assembly SqliteAssembly = typeof(Sqlite.SqliteConnectionFactory).Assembly;
    private static readonly Assembly ResultAssembly = typeof(Result.TransactionResultExtensions).Assembly;
    private static readonly Assembly TestingAssembly = typeof(Testing.FakeTransactionManager).Assembly;

    [Fact]
    public void Abstractions_ShouldNotDependOn_CoreOrAdapters()
    {
        TestResult result = Types.InAssembly(AbstractionsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "EricksonLopez.Transaction.Core",
                "EricksonLopez.Transaction.Dapper",
                "EricksonLopez.Transaction.PostgreSql",
                "EricksonLopez.Transaction.SqlServer",
                "EricksonLopez.Transaction.MySql",
                "EricksonLopez.Transaction.MariaDb",
                "EricksonLopez.Transaction.Oracle",
                "EricksonLopez.Transaction.Sqlite",
                "EricksonLopez.Transaction.Result",
                "Dapper",
                "Npgsql",
                "Microsoft.Data.SqlClient",
                "MySqlConnector",
                "Oracle.ManagedDataAccess.Client",
                "Microsoft.Data.Sqlite")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Core_ShouldNotDependOn_DapperOrDialects()
    {
        TestResult result = Types.InAssembly(CoreAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "EricksonLopez.Transaction.Dapper",
                "EricksonLopez.Transaction.PostgreSql",
                "EricksonLopez.Transaction.SqlServer",
                "EricksonLopez.Transaction.MySql",
                "EricksonLopez.Transaction.MariaDb",
                "EricksonLopez.Transaction.Oracle",
                "EricksonLopez.Transaction.Sqlite",
                "EricksonLopez.Transaction.Result",
                "Dapper",
                "Npgsql",
                "Microsoft.Data.SqlClient",
                "MySqlConnector",
                "Oracle.ManagedDataAccess.Client")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Dapper_ShouldNotDependOn_Dialects()
    {
        TestResult result = Types.InAssembly(DapperAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "EricksonLopez.Transaction.PostgreSql",
                "EricksonLopez.Transaction.SqlServer",
                "EricksonLopez.Transaction.MySql",
                "EricksonLopez.Transaction.MariaDb",
                "EricksonLopez.Transaction.Oracle",
                "Npgsql",
                "Microsoft.Data.SqlClient",
                "MySqlConnector",
                "Oracle.ManagedDataAccess.Client")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void ResultIntegration_ShouldNotDependOn_DapperOrDialects()
    {
        TestResult result = Types.InAssembly(ResultAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "EricksonLopez.Transaction.Dapper",
                "EricksonLopez.Transaction.PostgreSql",
                "EricksonLopez.Transaction.SqlServer",
                "EricksonLopez.Transaction.MySql",
                "EricksonLopez.Transaction.MariaDb",
                "EricksonLopez.Transaction.Oracle",
                "Dapper",
                "Npgsql",
                "Microsoft.Data.SqlClient",
                "MySqlConnector",
                "Oracle.ManagedDataAccess.Client")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void AllAssemblies_ShouldHaveConsistentNamingAndCopyright()
    {
        AbstractionsAssembly.GetName().Name.Should().Be("EricksonLopez.Transaction.Abstractions");
        CoreAssembly.GetName().Name.Should().Be("EricksonLopez.Transaction");
        DapperAssembly.GetName().Name.Should().Be("EricksonLopez.Transaction.Dapper");
        PostgreSqlAssembly.GetName().Name.Should().Be("EricksonLopez.Transaction.PostgreSql");
        SqlServerAssembly.GetName().Name.Should().Be("EricksonLopez.Transaction.SqlServer");
        MySqlAssembly.GetName().Name.Should().Be("EricksonLopez.Transaction.MySql");
        MariaDbAssembly.GetName().Name.Should().Be("EricksonLopez.Transaction.MariaDb");
        OracleAssembly.GetName().Name.Should().Be("EricksonLopez.Transaction.Oracle");
        SqliteAssembly.GetName().Name.Should().Be("EricksonLopez.Transaction.Sqlite");
        ResultAssembly.GetName().Name.Should().Be("EricksonLopez.Transaction.Result");
        TestingAssembly.GetName().Name.Should().Be("EricksonLopez.Transaction.Testing");
    }
}
