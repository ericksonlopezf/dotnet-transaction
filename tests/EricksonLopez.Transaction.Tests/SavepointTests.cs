// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Transaction.Diagnostics;
using EricksonLopez.Transaction.Internal;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Transaction.Tests;

public sealed class SavepointTests
{
    private sealed class MockDbConnection : DbConnection
    {
        public DbCommand? LastCreatedCommand { get; private set; }

        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "TestDb";
        public override string DataSource => "localhost";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => throw new NotImplementedException();

        protected override DbCommand CreateDbCommand()
        {
            var cmd = new MockDbCommand();
            LastCreatedCommand = cmd;
            return cmd;
        }
    }

    private sealed class MockDbCommand : DbCommand
    {
        [AllowNull]
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection => throw new NotImplementedException();
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery() => 0;
        public override object? ExecuteScalar() => null;
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => throw new NotImplementedException();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotImplementedException();
        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private sealed class ThrowingDbCommand : DbCommand
    {
        [AllowNull]
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection => throw new NotImplementedException();
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery() => throw new InvalidOperationException("SQL Server syntax error on release savepoint");
        public override object? ExecuteScalar() => null;
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => throw new NotImplementedException();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotImplementedException();
        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => Task.FromException<int>(new InvalidOperationException("SQL Server syntax error on release savepoint"));
    }

    private sealed class ThrowingCommandDbConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "TestDb";
        public override string DataSource => "localhost";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => throw new NotImplementedException();

        protected override DbCommand CreateDbCommand() => new ThrowingDbCommand();
    }

    private sealed class SavepointThrowingTransaction : DbTransaction
    {
        private readonly DbConnection? _conn;

        public SavepointThrowingTransaction(DbConnection? conn)
        {
            _conn = conn;
        }

        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection? DbConnection => _conn;

        public override void Commit() { }
        public override void Rollback() { }

        public override Task RollbackAsync(string savepointName, CancellationToken cancellationToken = default)
            => Task.FromException(new NotSupportedException("Driver does not support direct savepoint rollback API"));

        public override Task ReleaseAsync(string savepointName, CancellationToken cancellationToken = default)
            => Task.FromException(new NotSupportedException("Driver does not support direct savepoint release API"));
    }

    [Fact]
    public void Constructor_WhenTransactionIsNull_ShouldThrowArgumentNullException()
    {
        Action act = () => _ = new Savepoint(null!, "sp1");
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WhenNameIsEmptyOrNull_ShouldThrowArgumentException(string? name)
    {
        var dbTx = Substitute.For<DbTransaction>();
        Action act = () => _ = new Savepoint(dbTx, name!);
        act.Should().Throw<ArgumentException>().WithMessage("Savepoint name must not be empty.*");
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var dbTx = Substitute.For<DbTransaction>();
        var sp = new Savepoint(dbTx, "sp1");

        sp.Name.Should().Be("sp1");
        sp.IsRolledBack.Should().BeFalse();
        sp.IsReleased.Should().BeFalse();
    }

    [Fact]
    public async Task RollbackAsync_WhenDriverSupportsSavepoint_ShouldSucceed()
    {
        var recorded = new List<string>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == TransactionDiagnostics.SourceName) listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<long>((inst, val, tags, state) => recorded.Add(inst.Name));
        meterListener.Start();

        var dbTx = Substitute.For<DbTransaction>();
        var sp = new Savepoint(dbTx, "sp_test");

        await sp.RollbackAsync(CancellationToken.None);

        sp.IsRolledBack.Should().BeTrue();
        sp.IsReleased.Should().BeFalse();
        recorded.Should().Contain("transactions.savepoints.rolled_back");
    }

    [Fact]
    public async Task RollbackAsync_WhenDriverThrowsNotSupported_ShouldExecuteFallbackCommand()
    {
        var recorded = new List<string>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == TransactionDiagnostics.SourceName) listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<long>((inst, val, tags, state) => recorded.Add(inst.Name));
        meterListener.Start();

        var conn = new MockDbConnection();
        var tx = new SavepointThrowingTransaction(conn);
        var sp = new Savepoint(tx, "sp_fallback");

        await sp.RollbackAsync(CancellationToken.None);

        sp.IsRolledBack.Should().BeTrue();
        sp.IsReleased.Should().BeFalse();
        conn.LastCreatedCommand.Should().NotBeNull();
        conn.LastCreatedCommand!.CommandText.Should().Be("ROLLBACK TO SAVEPOINT sp_fallback;");
        recorded.Should().Contain("transactions.savepoints.rolled_back");
    }

    [Fact]
    public async Task RollbackAsync_WhenConnectionNullAndNotSupported_ShouldCompleteWithoutThrowing()
    {
        var tx = new SavepointThrowingTransaction(null);
        var sp = new Savepoint(tx, "sp_nullconn");

        await sp.RollbackAsync(CancellationToken.None);

        sp.IsRolledBack.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseAsync_WhenDriverSupportsRelease_ShouldSucceed()
    {
        var recorded = new List<string>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == TransactionDiagnostics.SourceName) listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<long>((inst, val, tags, state) => recorded.Add(inst.Name));
        meterListener.Start();

        var dbTx = Substitute.For<DbTransaction>();
        var sp = new Savepoint(dbTx, "sp_test");

        await sp.ReleaseAsync(CancellationToken.None);

        sp.IsReleased.Should().BeTrue();
        sp.IsRolledBack.Should().BeFalse();
        recorded.Should().Contain("transactions.savepoints.released");
    }

    [Fact]
    public async Task ReleaseAsync_WhenDriverThrowsNotSupported_ShouldExecuteFallbackCommand()
    {
        var recorded = new List<string>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == TransactionDiagnostics.SourceName) listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<long>((inst, val, tags, state) => recorded.Add(inst.Name));
        meterListener.Start();

        var conn = new MockDbConnection();
        var tx = new SavepointThrowingTransaction(conn);
        var sp = new Savepoint(tx, "sp_release");

        await sp.ReleaseAsync(CancellationToken.None);

        sp.IsReleased.Should().BeTrue();
        sp.IsRolledBack.Should().BeFalse();
        conn.LastCreatedCommand.Should().NotBeNull();
        conn.LastCreatedCommand!.CommandText.Should().Be("RELEASE SAVEPOINT sp_release;");
        recorded.Should().Contain("transactions.savepoints.released");
    }

    [Fact]
    public async Task ReleaseAsync_WhenConnectionNullAndNotSupported_ShouldCompleteWithoutThrowing()
    {
        var tx = new SavepointThrowingTransaction(null);
        var sp = new Savepoint(tx, "sp_nullconn");

        await sp.ReleaseAsync(CancellationToken.None);

        sp.IsReleased.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseAsync_WhenFallbackThrowsSqlException_ShouldSwallowSilently()
    {
        var conn = new ThrowingCommandDbConnection();
        var tx = new SavepointThrowingTransaction(conn);
        var sp = new Savepoint(tx, "sp_sql_server");

        Func<Task> act = () => sp.ReleaseAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        sp.IsReleased.Should().BeFalse();
    }

    [Fact]
    public async Task Operations_AfterDisposal_ShouldThrowObjectDisposedException()
    {
        var dbTx = Substitute.For<DbTransaction>();
        var sp = new Savepoint(dbTx, "sp_disposed");

        await sp.DisposeAsync();

        // Idempotent double dispose
        await sp.DisposeAsync();

        Func<Task> rollbackAct = () => sp.RollbackAsync(CancellationToken.None);
        Func<Task> releaseAct = () => sp.ReleaseAsync(CancellationToken.None);

        await rollbackAct.Should().ThrowAsync<ObjectDisposedException>();
        await releaseAct.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WhenNameEmptyOrWhitespace_ShouldThrowArgumentException(string? name)
    {
        var dbTx = Substitute.For<DbTransaction>();
        Action act = () => _ = new Savepoint(dbTx, name!);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*must not be empty*");
    }

    [Theory]
    [InlineData("sp with space")]
    [InlineData("sp-dash")]
    [InlineData("sp$dollar")]
    [InlineData("sp;semi")]
    [InlineData("sp!excl")]
    public void Constructor_WhenNameContainsInvalidCharacters_ShouldThrowArgumentException(string name)
    {
        var dbTx = Substitute.For<DbTransaction>();
        Action act = () => _ = new Savepoint(dbTx, name);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*contains invalid characters*");
    }
}
