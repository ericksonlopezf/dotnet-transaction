// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;
using EricksonLopez.Transaction.Oracle;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.Transaction.Oracle.Tests;

public sealed class OracleTransactionExtensionsTests
{
    private const string TestConnectionString = "Data Source=localhost:1521/XEPDB1;User Id=system;Password=SecretPassword123!;";

    [Fact]
    public void AddOracleTransaction_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        services.AddOracleTransaction(TestConnectionString);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetService<IDbConnectionFactory>();
        var manager = provider.GetService<ITransactionManager>();

        factory.Should().NotBeNull();
        manager.Should().NotBeNull();
    }

    [Fact]
    public void AddOracleTransaction_WhenArgumentsNull_ShouldThrowExceptions()
    {
        IServiceCollection nullServices = null!;

        Action act1 = () => nullServices.AddOracleTransaction(TestConnectionString);
        Action act2 = () => new ServiceCollection().AddOracleTransaction(null!);
        Action act3 = () => new ServiceCollection().AddOracleTransaction("   ");

        act1.Should().Throw<ArgumentNullException>().WithParameterName("services");
        act2.Should().Throw<ArgumentException>().WithParameterName("connectionString");
        act3.Should().Throw<ArgumentException>().WithParameterName("connectionString");
    }
}
