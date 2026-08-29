// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using EricksonLopez.Transaction;
using Microsoft.Data.Sqlite;

namespace EricksonLopez.Transaction.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class TransactionScopeBenchmarks : IDisposable
{
    private SqliteConnection _connection = null!;
    private ITransactionManager _manager = null!;

    [GlobalSetup]
    public void Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE scope_benchmarks (id INT, name TEXT);";
        cmd.ExecuteNonQuery();

        _manager = new TransactionManager(new DelegateDbConnectionFactory(() => _connection));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _connection.Dispose();
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    [Benchmark(Baseline = true)]
    public async Task SingleTransactionScopeExecution()
    {
        await _manager.ExecuteAsync(async ctx =>
        {
            await using var cmd = ctx.Connection.CreateCommand();
            cmd.Transaction = ctx.Transaction;
            cmd.CommandText = "INSERT INTO scope_benchmarks VALUES (1, 'Single');";
            await cmd.ExecuteNonQueryAsync();
        });
    }

    [Benchmark]
    public async Task NestedSavepointTransactionExecution()
    {
        await _manager.ExecuteAsync(async outerCtx =>
        {
            await _manager.ExecuteAsync(async innerCtx =>
            {
                await using var cmd = innerCtx.Connection.CreateCommand();
                cmd.Transaction = innerCtx.Transaction;
                cmd.CommandText = "INSERT INTO scope_benchmarks VALUES (2, 'NestedSavepoint');";
                await cmd.ExecuteNonQueryAsync();
            }, new TransactionOptions { NestedBehavior = NestedTransactionBehavior.UseSavepoint });
        });
    }

    [Benchmark]
    public async Task JoinExistingTransactionExecution()
    {
        await _manager.ExecuteAsync(async outerCtx =>
        {
            await _manager.ExecuteAsync(async innerCtx =>
            {
                await using var cmd = innerCtx.Connection.CreateCommand();
                cmd.Transaction = innerCtx.Transaction;
                cmd.CommandText = "INSERT INTO scope_benchmarks VALUES (3, 'JoinExisting');";
                await cmd.ExecuteNonQueryAsync();
            }, new TransactionOptions { NestedBehavior = NestedTransactionBehavior.JoinExisting });
        });
    }
}
