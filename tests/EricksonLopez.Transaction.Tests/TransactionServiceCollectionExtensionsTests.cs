// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Transaction;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.Transaction.Tests;

public sealed class TransactionServiceCollectionExtensionsTests
{
    private sealed class CustomTestConnectionFactory : IDbConnectionFactory
    {
        public DbConnection CreateConnection() => new SqliteConnection("Data Source=:memory:");
        public ValueTask<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<DbConnection>(new SqliteConnection("Data Source=:memory:"));
    }

    [Fact]
    public void AddTransaction_Generic_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        var returned = services.AddTransaction<CustomTestConnectionFactory>();
        returned.Should().BeSameAs(services);

        using var provider = services.BuildServiceProvider();
        var resolvedFactory = provider.GetService<IDbConnectionFactory>();
        var resolvedManager = provider.GetService<ITransactionManager>();

        resolvedFactory.Should().NotBeNull();
        resolvedFactory.Should().BeOfType<CustomTestConnectionFactory>();
        resolvedManager.Should().NotBeNull();
        resolvedManager.Should().BeOfType<TransactionManager>();
    }

    [Fact]
    public void AddTransaction_WithResolver_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        var returned = services.AddTransaction(sp => new CustomTestConnectionFactory());
        returned.Should().BeSameAs(services);

        using var provider = services.BuildServiceProvider();
        var resolvedFactory = provider.GetService<IDbConnectionFactory>();
        var resolvedManager = provider.GetService<ITransactionManager>();

        resolvedFactory.Should().NotBeNull();
        resolvedFactory.Should().BeOfType<CustomTestConnectionFactory>();
        resolvedManager.Should().NotBeNull();
        resolvedManager.Should().BeOfType<TransactionManager>();
    }

    [Fact]
    public void AddTransaction_WithAsyncDelegate_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        var returned = services.AddTransaction((sp, ct) => ValueTask.FromResult<DbConnection>(new SqliteConnection("Data Source=:memory:")));
        returned.Should().BeSameAs(services);

        using var provider = services.BuildServiceProvider();
        var resolvedFactory = provider.GetService<IDbConnectionFactory>();
        var resolvedManager = provider.GetService<ITransactionManager>();

        resolvedFactory.Should().NotBeNull();
        resolvedFactory.Should().BeOfType<DelegateDbConnectionFactory>();
        resolvedManager.Should().NotBeNull();
        resolvedManager.Should().BeOfType<TransactionManager>();
    }

    [Fact]
    public void AddTransaction_WithSyncDelegate_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        var returned = services.AddTransaction(sp => (DbConnection)new SqliteConnection("Data Source=:memory:"));
        returned.Should().BeSameAs(services);

        using var provider = services.BuildServiceProvider();
        var resolvedFactory = provider.GetService<IDbConnectionFactory>();
        var resolvedManager = provider.GetService<ITransactionManager>();

        resolvedFactory.Should().NotBeNull();
        resolvedFactory.Should().BeOfType<DelegateDbConnectionFactory>();
        resolvedManager.Should().NotBeNull();
        resolvedManager.Should().BeOfType<TransactionManager>();
    }

    [Fact]
    public void AddTransaction_WhenArgumentsNull_ShouldThrowArgumentNullException()
    {
        IServiceCollection nullServices = null!;
        var services = new ServiceCollection();

        Action act1 = () => nullServices.AddTransaction<CustomTestConnectionFactory>();
        Action act2 = () => nullServices.AddTransaction(sp => new CustomTestConnectionFactory());
        Action act3 = () => services.AddTransaction((Func<IServiceProvider, IDbConnectionFactory>)null!);
        Action act4 = () => nullServices.AddTransaction((sp, ct) => ValueTask.FromResult<DbConnection>(new SqliteConnection("Data Source=:memory:")));
        Action act5 = () => services.AddTransaction((Func<IServiceProvider, CancellationToken, ValueTask<DbConnection>>)null!);
        Action act6 = () => nullServices.AddTransaction(sp => (DbConnection)new SqliteConnection("Data Source=:memory:"));
        Action act7 = () => services.AddTransaction((Func<IServiceProvider, DbConnection>)null!);

        act1.Should().Throw<ArgumentNullException>().WithParameterName("services");
        act2.Should().Throw<ArgumentNullException>().WithParameterName("services");
        act3.Should().Throw<ArgumentNullException>().WithParameterName("factoryResolver");
        act4.Should().Throw<ArgumentNullException>().WithParameterName("services");
        act5.Should().Throw<ArgumentNullException>().WithParameterName("connectionProvider");
        act6.Should().Throw<ArgumentNullException>().WithParameterName("services");
        act7.Should().Throw<ArgumentNullException>().WithParameterName("connectionProvider");
    }
}
