// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;
using EricksonLopez.Transaction.MySql;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.Transaction.MySql.Tests;

public sealed class MySqlTransactionExtensionsTests
{
    private const string TestConnectionString = "Server=localhost;Port=3306;Database=TestDb;Uid=root;Pwd=SecretPassword123!;";

    [Fact]
    public void AddMySqlTransaction_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        services.AddMySqlTransaction(TestConnectionString);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetService<IDbConnectionFactory>();
        var manager = provider.GetService<ITransactionManager>();

        factory.Should().NotBeNull();
        manager.Should().NotBeNull();
    }

    [Fact]
    public void AddMySqlTransaction_WhenArgumentsNull_ShouldThrowExceptions()
    {
        IServiceCollection nullServices = null!;

        Action act1 = () => nullServices.AddMySqlTransaction(TestConnectionString);
        Action act2 = () => new ServiceCollection().AddMySqlTransaction(null!);
        Action act3 = () => new ServiceCollection().AddMySqlTransaction("   ");

        act1.Should().Throw<ArgumentNullException>().WithParameterName("services");
        act2.Should().Throw<ArgumentException>().WithParameterName("connectionString");
        act3.Should().Throw<ArgumentException>().WithParameterName("connectionString");
    }
}
