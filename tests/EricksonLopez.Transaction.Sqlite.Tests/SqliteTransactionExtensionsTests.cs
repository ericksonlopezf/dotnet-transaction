// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;
using EricksonLopez.Transaction.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.Transaction.Sqlite.Tests;

public sealed class SqliteTransactionExtensionsTests
{
    private const string TestConnectionString = "Data Source=:memory:";

    [Fact]
    public void AddSqliteTransaction_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        services.AddSqliteTransaction(TestConnectionString);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetService<IDbConnectionFactory>();
        var manager = provider.GetService<ITransactionManager>();

        factory.Should().NotBeNull();
        manager.Should().NotBeNull();
    }

    [Fact]
    public void AddSqliteTransaction_WhenArgumentsNull_ShouldThrowExceptions()
    {
        IServiceCollection nullServices = null!;

        Action act1 = () => nullServices.AddSqliteTransaction(TestConnectionString);
        Action act2 = () => new ServiceCollection().AddSqliteTransaction(null!);
        Action act3 = () => new ServiceCollection().AddSqliteTransaction("   ");

        act1.Should().Throw<ArgumentNullException>().WithParameterName("services");
        act2.Should().Throw<ArgumentException>().WithParameterName("connectionString");
        act3.Should().Throw<ArgumentException>().WithParameterName("connectionString");
    }
}
