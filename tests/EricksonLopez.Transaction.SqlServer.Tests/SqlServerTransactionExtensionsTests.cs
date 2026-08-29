// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;
using EricksonLopez.Transaction.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.Transaction.SqlServer.Tests;

public sealed class SqlServerTransactionExtensionsTests
{
    private const string TestConnectionString = "Server=tcp:localhost,1433;Database=TestDb;User Id=sa;Password=SecretPassword123!;Encrypt=True;TrustServerCertificate=True;";

    [Fact]
    public void AddSqlServerTransaction_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        services.AddSqlServerTransaction(TestConnectionString);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetService<IDbConnectionFactory>();
        var manager = provider.GetService<ITransactionManager>();

        factory.Should().NotBeNull();
        manager.Should().NotBeNull();
    }

    [Fact]
    public void AddSqlServerTransaction_WhenArgumentsNull_ShouldThrowExceptions()
    {
        IServiceCollection nullServices = null!;

        Action act1 = () => nullServices.AddSqlServerTransaction(TestConnectionString);
        Action act2 = () => new ServiceCollection().AddSqlServerTransaction(null!);
        Action act3 = () => new ServiceCollection().AddSqlServerTransaction("   ");

        act1.Should().Throw<ArgumentNullException>().WithParameterName("services");
        act2.Should().Throw<ArgumentException>().WithParameterName("connectionString");
        act3.Should().Throw<ArgumentException>().WithParameterName("connectionString");
    }
}
