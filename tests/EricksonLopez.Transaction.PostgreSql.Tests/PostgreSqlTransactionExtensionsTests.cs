// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace EricksonLopez.Transaction.PostgreSql.Tests;

public sealed class PostgreSqlTransactionExtensionsTests
{
    [Fact]
    public void AddPostgreSqlTransaction_WithConnectionString_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        services.AddPostgreSqlTransaction("Host=localhost;Database=test;Username=postgres;Password=postgres");

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetService<IDbConnectionFactory>();
        var manager = provider.GetService<ITransactionManager>();
        var ds = provider.GetService<NpgsqlDataSource>();

        factory.Should().NotBeNull();
        manager.Should().NotBeNull();
        ds.Should().NotBeNull();
    }

    [Fact]
    public void AddPostgreSqlTransaction_WithDataSource_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=test;Username=postgres;Password=postgres");
        services.AddPostgreSqlTransaction(dataSource);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetService<IDbConnectionFactory>();
        var manager = provider.GetService<ITransactionManager>();
        var ds = provider.GetService<NpgsqlDataSource>();

        factory.Should().NotBeNull();
        manager.Should().NotBeNull();
        ds.Should().BeSameAs(dataSource);
    }

    [Fact]
    public void AddPostgreSqlTransaction_WhenArgumentsNull_ShouldThrowExceptions()
    {
        IServiceCollection nullServices = null!;
        NpgsqlDataSource nullDataSource = null!;

        Action act1 = () => nullServices.AddPostgreSqlTransaction("Host=localhost");
        Action act2 = () => new ServiceCollection().AddPostgreSqlTransaction((string)null!);
        Action act3 = () => new ServiceCollection().AddPostgreSqlTransaction("   ");
        Action act4 = () => nullServices.AddPostgreSqlTransaction(NpgsqlDataSource.Create("Host=localhost"));
        Action act5 = () => new ServiceCollection().AddPostgreSqlTransaction(nullDataSource);

        act1.Should().Throw<ArgumentNullException>().WithParameterName("services");
        act2.Should().Throw<ArgumentException>();
        act3.Should().Throw<ArgumentException>();
        act4.Should().Throw<ArgumentNullException>().WithParameterName("services");
        act5.Should().Throw<ArgumentNullException>().WithParameterName("dataSource");
    }
}
