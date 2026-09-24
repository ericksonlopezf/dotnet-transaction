// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Transaction.Dialects;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Transaction.Tests
{
    public sealed class DialectResolverTests
    {
        [Fact]
        public void Resolve_WhenCustomDialectCanHandle_ShouldReturnCustomDialect()
        {
            var customDialect = Substitute.For<IDatabaseDialect>();
            using var conn = new DummyConnection();
            customDialect.CanHandle(conn).Returns(true);

            var resolved = DialectResolver.Resolve(conn, [customDialect]);

            resolved.Should().BeSameAs(customDialect);
        }

        [Fact]
        public void Resolve_WhenCustomDialectCannotHandle_ShouldFallBackToDefaultDialects()
        {
            var customDialect = Substitute.For<IDatabaseDialect>();
            using var conn = new OracleFakeConnection();
            customDialect.CanHandle(conn).Returns(false);

            var resolved = DialectResolver.Resolve(conn, [customDialect]);

            resolved.Should().BeOfType<OracleDialect>();
        }

        [Fact]
        public void Resolve_WhenOracleConnection_ShouldReturnOracleDialect()
        {
            using var conn = new OracleFakeConnection();

            var resolved = DialectResolver.Resolve(conn);

            resolved.Should().BeOfType<OracleDialect>();
        }

        [Fact]
        public void Resolve_WhenOracleClientNamespaceConnection_ShouldReturnOracleDialect()
        {
            using var conn = new Oracle.ManagedDataAccess.Client.OracleTestClientConnection();

            var resolved = DialectResolver.Resolve(conn);

            resolved.Should().BeOfType<OracleDialect>();
        }

        [Fact]
        public void Resolve_WhenUnrecognizedConnection_ShouldReturnGenericSqlDialect()
        {
            using var conn = new DummyConnection();

            var resolved = DialectResolver.Resolve(conn);

            resolved.Should().BeSameAs(GenericSqlDialect.Instance);
        }

        [Fact]
        public void OracleDialect_SavepointMethods_ShouldMatchOracleSpecification()
        {
            var dialect = new OracleDialect();

            dialect.GetSavepointCreationSql("sp_test").Should().Be("SAVEPOINT sp_test;");
            dialect.GetSavepointRollbackSql("sp_test").Should().Be("ROLLBACK TO SAVEPOINT sp_test;");
            dialect.GetSavepointReleaseSql("sp_test").Should().BeNull();
        }

        [Fact]
        public void OracleDialect_CanHandle_CoversIndexBoundary()
        {
            var dialect = new OracleDialect();
            using var indexZero = new Oracle.ManagedDataAccess.Client.OracleTestClientConnection();
            using var indexPositive = new Custom.Oracle.Driver.OracleDriverConnection();
            using var notOracle = new DummyConnection();

            dialect.CanHandle(indexZero).Should().BeTrue();
            dialect.CanHandle(indexPositive).Should().BeTrue();
            dialect.CanHandle(notOracle).Should().BeFalse();
        }

        [Fact]
        public async Task OracleDialect_ApplyReadOnlyModeAsync_ShouldExecuteSetTransactionReadOnly()
        {
            var dialect = new OracleDialect();
            using var conn = new OracleFakeConnection();
            using var tx = new FakeDbTransaction(conn);

            await dialect.ApplyReadOnlyModeAsync(conn, tx, CancellationToken.None);

            conn.LastCreatedCommand.Should().NotBeNull();
            conn.LastCreatedCommand!.CommandText.Should().Be("SET TRANSACTION READ ONLY;");
            conn.LastCreatedCommand.Transaction.Should().BeSameAs(tx);
        }

        [Fact]
        public async Task GenericSqlDialect_Methods_ShouldBehaveCorrectly()
        {
            var dialect = GenericSqlDialect.Instance;
            using var conn = new DummyConnection();
            using var tx = new FakeDbTransaction(conn);

            dialect.CanHandle(conn).Should().BeTrue();
            await dialect.ApplyReadOnlyModeAsync(conn, tx, CancellationToken.None);
            dialect.GetSavepointCreationSql("sp1").Should().Be("SAVEPOINT sp1;");
            dialect.GetSavepointRollbackSql("sp1").Should().Be("ROLLBACK TO SAVEPOINT sp1;");
            dialect.GetSavepointReleaseSql("sp1").Should().Be("RELEASE SAVEPOINT sp1;");
        }

        [Fact]
        public async Task MySqlDialect_Methods_ShouldBehaveCorrectly()
        {
            var dialect = new MySqlDialect();
            using var connConnector = new MySqlConnector.MySqlConnectorConnection();
            using var connClassic = new MySql.Data.MySqlClient.MySqlClientConnection();
            using var dummyConn = new DummyConnection();

            dialect.CanHandle(connConnector).Should().BeTrue();
            dialect.CanHandle(connClassic).Should().BeTrue();
            dialect.CanHandle(dummyConn).Should().BeFalse();

            dialect.GetSavepointCreationSql("sp1").Should().Be("SAVEPOINT sp1;");
            dialect.GetSavepointRollbackSql("sp1").Should().Be("ROLLBACK TO SAVEPOINT sp1;");
            dialect.GetSavepointReleaseSql("sp1").Should().Be("RELEASE SAVEPOINT sp1;");

            using var fakeConn = new OracleFakeConnection();
            using var tx = new FakeDbTransaction(fakeConn);
            await dialect.ApplyReadOnlyModeAsync(fakeConn, tx, CancellationToken.None);
            fakeConn.LastCreatedCommand.Should().NotBeNull();
            fakeConn.LastCreatedCommand!.CommandText.Should().Be("SET TRANSACTION READ ONLY;");
        }

        [Fact]
        public void OracleDialect_CanHandle_ShouldRecognizeAllMatchingNamespaces()
        {
            var dialect = new OracleDialect();
            using var connManaged = new Oracle.ManagedDataAccess.Client.OracleTestClientConnection();
            using var connOther = new Oracle.Other.OracleOtherConnection();
            using var connCustom = new Custom.Oracle.Driver.OracleDriverConnection();
            using var dummy = new DummyConnection();

            dialect.CanHandle(connManaged).Should().BeTrue();
            dialect.CanHandle(connOther).Should().BeTrue();
            dialect.CanHandle(connCustom).Should().BeTrue();
            dialect.CanHandle(dummy).Should().BeFalse();
        }

        [Fact]
        public async Task PostgreSqlDialect_Methods_ShouldBehaveCorrectly()
        {
            var dialect = new PostgreSqlDialect();
            using var connNpgsql = new Npgsql.NpgsqlTestConnection();
            using var dummy = new DummyConnection();

            dialect.CanHandle(connNpgsql).Should().BeTrue();
            dialect.CanHandle(dummy).Should().BeFalse();

            dialect.GetSavepointCreationSql("sp1").Should().Be("SAVEPOINT sp1;");
            dialect.GetSavepointRollbackSql("sp1").Should().Be("ROLLBACK TO SAVEPOINT sp1;");
            dialect.GetSavepointReleaseSql("sp1").Should().Be("RELEASE SAVEPOINT sp1;");

            using var fakeConn = new OracleFakeConnection();
            using var tx = new FakeDbTransaction(fakeConn);
            await dialect.ApplyReadOnlyModeAsync(fakeConn, tx, CancellationToken.None);
            fakeConn.LastCreatedCommand.Should().NotBeNull();
            fakeConn.LastCreatedCommand!.CommandText.Should().Be("SET TRANSACTION READ ONLY;");
        }

        [Fact]
        public async Task SqliteDialect_Methods_ShouldBehaveCorrectly()
        {
            var dialect = new SqliteDialect();
            using var connMs = new Microsoft.Data.Sqlite.SqliteMsConnection();
            using var connSys = new System.Data.SQLite.SqliteSysConnection();
            using var dummy = new DummyConnection();

            dialect.CanHandle(connMs).Should().BeTrue();
            dialect.CanHandle(connSys).Should().BeTrue();
            dialect.CanHandle(dummy).Should().BeFalse();

            dialect.GetSavepointCreationSql("sp1").Should().Be("SAVEPOINT sp1;");
            dialect.GetSavepointRollbackSql("sp1").Should().Be("ROLLBACK TO SAVEPOINT sp1;");
            dialect.GetSavepointReleaseSql("sp1").Should().Be("RELEASE SAVEPOINT sp1;");

            using var tx = new FakeDbTransaction(dummy);
            var task = dialect.ApplyReadOnlyModeAsync(dummy, tx, CancellationToken.None);
            task.IsCompletedSuccessfully.Should().BeTrue();
            await task;
        }

        [Fact]
        public async Task SqlServerDialect_Methods_ShouldBehaveCorrectly()
        {
            var dialect = new SqlServerDialect();
            using var connMs = new Microsoft.Data.SqlClient.SqlMsConnection();
            using var connSys = new System.Data.SqlClient.SqlSysConnection();
            using var dummy = new DummyConnection();

            dialect.CanHandle(connMs).Should().BeTrue();
            dialect.CanHandle(connSys).Should().BeTrue();
            dialect.CanHandle(dummy).Should().BeFalse();

            dialect.GetSavepointCreationSql("sp1").Should().Be("SAVE TRANSACTION sp1;");
            dialect.GetSavepointRollbackSql("sp1").Should().Be("ROLLBACK TRANSACTION sp1;");
            dialect.GetSavepointReleaseSql("sp1").Should().BeNull();

            using var tx = new FakeDbTransaction(dummy);
            var task = dialect.ApplyReadOnlyModeAsync(dummy, tx, CancellationToken.None);
            task.IsCompletedSuccessfully.Should().BeTrue();
            await task;
        }

        private sealed class DummyConnection : DbConnection
        {
            [AllowNull]
            public override string ConnectionString { get; set; } = string.Empty;
            public override string Database => "dummy";
            public override string DataSource => "localhost";
            public override string ServerVersion => "1.0";
            public override ConnectionState State => ConnectionState.Open;
            public override void ChangeDatabase(string databaseName) { }
            public override void Close() { }
            public override void Open() { }
            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
            protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
        }

        private sealed class FakeDbTransaction : DbTransaction
        {
            private readonly DbConnection _connection;
            public FakeDbTransaction(DbConnection connection) => _connection = connection;
            public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
            protected override DbConnection DbConnection => _connection;
            public override void Commit() { }
            public override void Rollback() { }
        }

        private sealed class FakeDbCommand : DbCommand
        {
            [AllowNull]
            public override string CommandText { get; set; } = string.Empty;
            public override int CommandTimeout { get; set; }
            public override CommandType CommandType { get; set; } = CommandType.Text;
            public override bool DesignTimeVisible { get; set; }
            public override UpdateRowSource UpdatedRowSource { get; set; }
            protected override DbConnection? DbConnection { get; set; }
            protected override DbParameterCollection DbParameterCollection => throw new NotSupportedException();
            protected override DbTransaction? DbTransaction { get; set; }
            public override void Cancel() { }
            public override int ExecuteNonQuery() => 0;
            public override object? ExecuteScalar() => null;
            public override void Prepare() { }
            protected override DbParameter CreateDbParameter() => throw new NotSupportedException();
            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
            public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        }

        private sealed class OracleFakeConnection : DbConnection
        {
            public FakeDbCommand? LastCreatedCommand { get; private set; }
            [AllowNull]
            public override string ConnectionString { get; set; } = string.Empty;
            public override string Database => "ORCL";
            public override string DataSource => "localhost:1521/XEPDB1";
            public override string ServerVersion => "19.0";
            public override ConnectionState State => ConnectionState.Open;
            public override void ChangeDatabase(string databaseName) { }
            public override void Close() { }
            public override void Open() { }
            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new FakeDbTransaction(this);
            protected override DbCommand CreateDbCommand()
            {
                var cmd = new FakeDbCommand();
                LastCreatedCommand = cmd;
                return cmd;
            }
        }
    }
}

namespace Oracle.ManagedDataAccess.Client
{
    internal sealed class OracleTestClientConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "ORCL";
        public override string DataSource => "localhost";
        public override string ServerVersion => "19.0";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }
}

namespace Oracle.Other
{
    internal sealed class OracleOtherConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "ORCL";
        public override string DataSource => "localhost";
        public override string ServerVersion => "19.0";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }
}

namespace Custom.Oracle.Driver
{
    internal sealed class OracleDriverConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "ORCL";
        public override string DataSource => "localhost";
        public override string ServerVersion => "19.0";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }
}

namespace MySqlConnector
{
    internal sealed class MySqlConnectorConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "db";
        public override string DataSource => "localhost";
        public override string ServerVersion => "8.0";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }
}

namespace MySql.Data.MySqlClient
{
    internal sealed class MySqlClientConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "db";
        public override string DataSource => "localhost";
        public override string ServerVersion => "8.0";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }
}

namespace Npgsql
{
    internal sealed class NpgsqlTestConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "pg";
        public override string DataSource => "localhost";
        public override string ServerVersion => "16.0";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }
}

namespace Microsoft.Data.Sqlite
{
    internal sealed class SqliteMsConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "main";
        public override string DataSource => "memory";
        public override string ServerVersion => "3.0";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }
}

namespace System.Data.SQLite
{
    internal sealed class SqliteSysConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "main";
        public override string DataSource => "memory";
        public override string ServerVersion => "3.0";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }
}

namespace Microsoft.Data.SqlClient
{
    internal sealed class SqlMsConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "master";
        public override string DataSource => "localhost";
        public override string ServerVersion => "16.0";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }
}

namespace System.Data.SqlClient
{
    internal sealed class SqlSysConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "master";
        public override string DataSource => "localhost";
        public override string ServerVersion => "16.0";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }
}
