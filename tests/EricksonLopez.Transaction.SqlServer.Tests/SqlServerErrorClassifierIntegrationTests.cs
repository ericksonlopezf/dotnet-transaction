// Copyright © Erickson Lopez. MIT License.
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Transaction;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Xunit;

namespace EricksonLopez.Transaction.SqlServer.Tests;

public class SqlServerErrorClassifierIntegrationTests : IAsyncLifetime
    {
        private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();

        public async Task InitializeAsync()
        {
            await _msSqlContainer.StartAsync();
            
            await using var conn = new SqlConnection(_msSqlContainer.GetConnectionString());
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
            await _msSqlContainer.DisposeAsync();
        }

        [Fact]
        public async Task Classifier_ShouldIdentifyDeadlock_WhenRealDeadlockOccurs()
        {
            // Arrange
            var connectionString = _msSqlContainer.GetConnectionString();
            
            var task1 = Task.Run(async () =>
            {
                await using var conn1 = new SqlConnection(connectionString);
                await conn1.OpenAsync();
                await using var tx1 = conn1.BeginTransaction();
                
                await using var cmd1 = conn1.CreateCommand();
                cmd1.Transaction = tx1;
                cmd1.CommandText = "UPDATE deadlock_test SET value = 101 WHERE id = 1";
                await cmd1.ExecuteNonQueryAsync();
                
                await Task.Delay(500); // Wait for tx2 to lock id 2
                
                cmd1.CommandText = "UPDATE deadlock_test SET value = 201 WHERE id = 2";
                await cmd1.ExecuteNonQueryAsync();
                tx1.Commit();
            });
            
            var task2 = Task.Run(async () =>
            {
                await using var conn2 = new SqlConnection(connectionString);
                await conn2.OpenAsync();
                await using var tx2 = conn2.BeginTransaction();
                
                await using var cmd2 = conn2.CreateCommand();
                cmd2.Transaction = tx2;
                cmd2.CommandText = "UPDATE deadlock_test SET value = 202 WHERE id = 2";
                await cmd2.ExecuteNonQueryAsync();
                
                await Task.Delay(500); // Wait for tx1 to lock id 1
                
                cmd2.CommandText = "UPDATE deadlock_test SET value = 102 WHERE id = 1";
                await cmd2.ExecuteNonQueryAsync();
                tx2.Commit();
            });

            // Act
            var exception = await Assert.ThrowsAnyAsync<SqlException>(() => Task.WhenAll(task1, task2));

            // Assert
            var isDeadlock = SqlServerErrorClassifier.IsDeadlock(exception);
            isDeadlock.Should().BeTrue();
        }
    }
