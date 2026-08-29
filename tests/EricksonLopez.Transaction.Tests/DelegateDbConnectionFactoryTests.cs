// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Transaction.Tests;

public sealed class DelegateDbConnectionFactoryTests
{
    private sealed class OpenTrackingConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;

        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "TestDb";
        public override string DataSource => "localhost";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { _state = ConnectionState.Closed; }
        public override void Open() { _state = ConnectionState.Open; }
        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            _state = ConnectionState.Open;
            return Task.CompletedTask;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => throw new NotImplementedException();

        protected override DbCommand CreateDbCommand()
            => throw new NotImplementedException();
    }

    [Fact]
    public void Constructor_WhenDelegatesNull_ShouldThrowArgumentNullException()
    {
        Action act1 = () => _ = new DelegateDbConnectionFactory((Func<CancellationToken, ValueTask<DbConnection>>)null!);
        Action act2 = () => _ = new DelegateDbConnectionFactory((Func<DbConnection>)null!);

        act1.Should().Throw<ArgumentNullException>().WithParameterName("asyncFactory");
        act2.Should().Throw<ArgumentNullException>().WithParameterName("syncFactory");
    }

    [Fact]
    public async Task AsyncFactory_ShouldCreateConnectionAsynchronously()
    {
        var conn = new OpenTrackingConnection();
        var factory = new DelegateDbConnectionFactory(ct => ValueTask.FromResult<DbConnection>(conn));

        DbConnection result = await factory.CreateConnectionAsync(CancellationToken.None);
        result.Should().BeSameAs(conn);

        Action syncCall = () => factory.CreateConnection();
        syncCall.Should().Throw<NotSupportedException>()
            .WithMessage("Synchronous connection creation is not supported when configured with an asynchronous factory.");
    }

    [Fact]
    public async Task SyncFactory_ShouldCreateConnectionSynchronouslyAndAsynchronouslyWithAutoOpen()
    {
        var conn = new OpenTrackingConnection();
        var factory = new DelegateDbConnectionFactory(() => conn);

        DbConnection syncResult = factory.CreateConnection();
        syncResult.Should().BeSameAs(conn);

        conn.Close();
        conn.State.Should().Be(ConnectionState.Closed);

        DbConnection asyncResult = await factory.CreateConnectionAsync(CancellationToken.None);
        asyncResult.Should().BeSameAs(conn);
        conn.State.Should().Be(ConnectionState.Open);
    }
}
