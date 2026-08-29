// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;
using EricksonLopez.Transaction.MariaDb;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.Transaction.MariaDb.Tests;

public sealed class MariaDbTransactionExtensionsTests
{
    private const string TestConnectionString = "Server=localhost;Port=3306;Database=TestDb;Uid=root;Pwd=SecretPassword123!;";

    [Fact]
    public void AddMariaDbTransaction_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        services.AddMariaDbTransaction(TestConnectionString);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetService<IDbConnectionFactory>();
        var manager = provider.GetService<ITransactionManager>();

        factory.Should().NotBeNull();
        manager.Should().NotBeNull();
    }

    [Fact]
    public void AddMariaDbTransaction_WhenArgumentsNull_ShouldThrowExceptions()
    {
        IServiceCollection nullServices = null!;

        Action act1 = () => nullServices.AddMariaDbTransaction(TestConnectionString);
        Action act2 = () => new ServiceCollection().AddMariaDbTransaction(null!);
        Action act3 = () => new ServiceCollection().AddMariaDbTransaction("   ");

        act1.Should().Throw<ArgumentNullException>().WithParameterName("services");
        act2.Should().Throw<ArgumentException>().WithParameterName("connectionString");
        act3.Should().Throw<ArgumentException>().WithParameterName("connectionString");
    }
}
