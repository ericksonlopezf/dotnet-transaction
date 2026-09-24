// Copyright © Erickson Lopez. MIT License.
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Transaction;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace EricksonLopez.Transaction.PostgreSql.Tests;

public class PostgreSqlErrorClassifierIntegrationTests : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgreSqlContainer = new PostgreSqlBuilder("postgres:15-alpine")
            .Build();

        public async Task InitializeAsync()
        {
            await _postgreSqlContainer.StartAsync();
            
            await using var conn = new NpgsqlConnection(_postgreSqlContainer.GetConnectionString());
            await conn.OpenAsync();
            
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE deadlock_test (id INT PRIMARY KEY, value INT);
                INSERT INTO deadlock_test (id, value) VALUES (1, 100), (2, 200);
            ";
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DisposeAsync()
        {
            await _postgreSqlContainer.DisposeAsync();
        }

        [Fact]
        public async Task Classifier_ShouldIdentifyDeadlock_WhenRealDeadlockOccurs()
        {
            // Arrange
            var connectionString = _postgreSqlContainer.GetConnectionString();
            
            var task1 = Task.Run(async () =>
            {
                await using var conn1 = new NpgsqlConnection(connectionString);
                await conn1.OpenAsync();
                await using var tx1 = await conn1.BeginTransactionAsync();
                
                await using var cmd1 = conn1.CreateCommand();
                cmd1.Transaction = tx1;
                cmd1.CommandText = "UPDATE deadlock_test SET value = 101 WHERE id = 1";
                await cmd1.ExecuteNonQueryAsync();
                
                await Task.Delay(500); // Wait for tx2 to lock id 2
                
                cmd1.CommandText = "UPDATE deadlock_test SET value = 201 WHERE id = 2";
                await cmd1.ExecuteNonQueryAsync();
                await tx1.CommitAsync();
            });
            
            var task2 = Task.Run(async () =>
            {
                await using var conn2 = new NpgsqlConnection(connectionString);
                await conn2.OpenAsync();
                await using var tx2 = await conn2.BeginTransactionAsync();
                
                await using var cmd2 = conn2.CreateCommand();
                cmd2.Transaction = tx2;
                cmd2.CommandText = "UPDATE deadlock_test SET value = 202 WHERE id = 2";
                await cmd2.ExecuteNonQueryAsync();
                
                await Task.Delay(500); // Wait for tx1 to lock id 1
                
                cmd2.CommandText = "UPDATE deadlock_test SET value = 102 WHERE id = 1";
                await cmd2.ExecuteNonQueryAsync();
                await tx2.CommitAsync();
            });

            // Act
            var exception = await Assert.ThrowsAnyAsync<PostgresException>(() => Task.WhenAll(task1, task2));

            // Assert
            var isDeadlock = PostgreSqlErrorClassifier.IsDeadlock(exception);
            isDeadlock.Should().BeTrue();
        }
    }
