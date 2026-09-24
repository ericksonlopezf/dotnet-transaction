// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Transaction;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace EricksonLopez.Transaction.EntityFrameworkCore.Tests;

public class DbContextTransactionExtensionsTests
{
    public class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
        public DbSet<TestEntity> TestEntities => Set<TestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestEntity>().ToTable("TestEntities").HasKey(e => e.Id);
        }
    }

    [Fact]
    public async Task UseTransactionAsync_ShouldEnlistDbContextInTransaction()
    {
        // Arrange
        var connectionString = $"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new TestDbContext(options);

        var transactionManager = new TransactionManager(new DelegateDbConnectionFactory(() => connection));
        await using var transaction = await transactionManager.BeginAsync();

        // Act
        await dbContext.UseTransactionAsync(transaction.Context);

        // Assert
        var currentTransaction = dbContext.Database.CurrentTransaction;
        currentTransaction.Should().NotBeNull();
        currentTransaction!.GetDbTransaction().Should().Be(transaction.Context.Transaction);
    }

    [Fact]
    public async Task UseTransactionAsync_NullDbContext_ThrowsArgumentNullException()
    {
        // Arrange
        DbContext nullDbContext = null!;
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var transactionManager = new TransactionManager(new DelegateDbConnectionFactory(() => connection));
        await using var transaction = await transactionManager.BeginAsync();

        // Act & Assert
        var act = () => nullDbContext.UseTransactionAsync(transaction.Context);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UseTransactionAsync_NullTransactionContext_ThrowsArgumentNullException()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new TestDbContext(options);

        // Act & Assert
        var act = () => dbContext.UseTransactionAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UseTransactionAsync_TransactionCommit_PersistsDatabaseWrites()
    {
        // Arrange - shared in-memory database kept alive by master connection
        var connectionString = $"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();

        await using (var createCmd = keepAlive.CreateCommand())
        {
            createCmd.CommandText = "CREATE TABLE TestEntities (Id INTEGER PRIMARY KEY, Name TEXT);";
            await createCmd.ExecuteNonQueryAsync();
        }

        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .Options;

        var transactionManager = new TransactionManager(new DelegateDbConnectionFactory(() => connection));

        // Act
        await transactionManager.ExecuteAsync(async context =>
        {
            await using var dbContext = new TestDbContext(options);
            await dbContext.UseTransactionAsync(context, context.CancellationToken);

            dbContext.TestEntities.Add(new TestEntity { Id = 1, Name = "CommittedEntity" });
            await dbContext.SaveChangesAsync(context.CancellationToken);
        });

        // Assert - Verify record was persisted in database
        await using var verifyConn = new SqliteConnection(connectionString);
        await verifyConn.OpenAsync();
        var verifyOptions = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(verifyConn).Options;
        await using (var verifyContext = new TestDbContext(verifyOptions))
        {
            var entity = await verifyContext.TestEntities.FindAsync(1);
            entity.Should().NotBeNull();
            entity!.Name.Should().Be("CommittedEntity");
        }
    }

    [Fact]
    public async Task UseTransactionAsync_TransactionRollback_DiscardsDatabaseWrites()
    {
        // Arrange - shared in-memory database kept alive by master connection
        var connectionString = $"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();

        await using (var createCmd = keepAlive.CreateCommand())
        {
            createCmd.CommandText = "CREATE TABLE TestEntities (Id INTEGER PRIMARY KEY, Name TEXT);";
            await createCmd.ExecuteNonQueryAsync();
        }

        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .Options;

        var transactionManager = new TransactionManager(new DelegateDbConnectionFactory(() => connection));

        // Act - Exception triggers auto-rollback
        var act = () => transactionManager.ExecuteAsync(async context =>
        {
            await using var dbContext = new TestDbContext(options);
            await dbContext.UseTransactionAsync(context, context.CancellationToken);

            dbContext.TestEntities.Add(new TestEntity { Id = 2, Name = "RolledBackEntity" });
            await dbContext.SaveChangesAsync(context.CancellationToken);

            throw new InvalidOperationException("Force rollback");
        });

        await act.Should().ThrowAsync<InvalidOperationException>();

        // Assert - Verify record was not persisted in database
        await using var verifyConn = new SqliteConnection(connectionString);
        await verifyConn.OpenAsync();
        var verifyOptions = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(verifyConn).Options;
        await using (var verifyContext = new TestDbContext(verifyOptions))
        {
            var entity = await verifyContext.TestEntities.FindAsync(2);
            entity.Should().BeNull();
        }
    }
}
