// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Result;
using EricksonLopez.Transaction.Dapper;
using EricksonLopez.Transaction.Result;
using Microsoft.Data.Sqlite;
using Xunit;
using ResultInstance = EricksonLopez.Result.Result;

namespace EricksonLopez.Transaction.IntegrationTests;

public sealed class TransactionIntegrationTests
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TransactionIntegrationTests()
    {
        // Using in-memory SQLite connection for isolated, deterministic relational database testing
        _connectionFactory = new DelegateDbConnectionFactory(async ct =>
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            await conn.OpenAsync(ct);
            return conn;
        });
    }

    [Fact]
    public async Task AtomicExecution_ShouldCommitAllOperationsTogether()
    {
        var manager = new TransactionManager(_connectionFactory);

        await manager.ExecuteAsync(async context =>
        {
            await context.ExecuteAsync("CREATE TABLE accounts (id INT PRIMARY KEY, balance REAL);");
            await context.ExecuteAsync("INSERT INTO accounts VALUES (1, 1000.00);");
            await context.ExecuteAsync("INSERT INTO accounts VALUES (2, 500.00);");

            // Transfer 200 from account 1 to account 2
            await context.ExecuteAsync("UPDATE accounts SET balance = balance - 200 WHERE id = 1;");
            await context.ExecuteAsync("UPDATE accounts SET balance = balance + 200 WHERE id = 2;");

            double bal1 = await context.ExecuteScalarAsync<double>("SELECT balance FROM accounts WHERE id = 1;");
            double bal2 = await context.ExecuteScalarAsync<double>("SELECT balance FROM accounts WHERE id = 2;");

            bal1.Should().Be(800.00);
            bal2.Should().Be(700.00);
        });
    }

    [Fact]
    public async Task AtomicExecution_WhenExceptionOccurs_ShouldRollbackAllChanges()
    {
        var manager = new TransactionManager(_connectionFactory);

        var act = () => manager.ExecuteAsync(async context =>
        {
            await context.ExecuteAsync("CREATE TABLE orders (id INT PRIMARY KEY, amount REAL);");
            await context.ExecuteAsync("INSERT INTO orders VALUES (1, 250.00);");

            // Simulated business failure during multi-entity operation
            throw new InvalidOperationException("Payment processing failed");
        });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Payment processing failed");
    }

    [Fact]
    public async Task ResultPattern_MultiEntitySave_WhenFailure_ShouldRollbackCompletely()
    {
        var manager = new TransactionManager(_connectionFactory);
        var validationError = Error.Validation("INVALID_INVENTORY", "Insufficient stock remaining.");

        ResultInstance result = await manager.ExecuteResultAsync(async context =>
        {
            await context.ExecuteAsync("CREATE TABLE inventory (sku TEXT PRIMARY KEY, qty INT);");
            await context.ExecuteAsync("INSERT INTO inventory VALUES ('ITEM-1', 10);");

            // Business condition fails
            return ResultInstance.Failure(validationError);
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(validationError);
    }
}
