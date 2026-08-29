// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using EricksonLopez.Transaction;
using EricksonLopez.Transaction.Internal;
using Microsoft.Data.Sqlite;

namespace EricksonLopez.Transaction.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class TransactionBenchmarks : IDisposable
{
    private SqliteConnection _connection = null!;
    private ITransactionManager _manager = null!;

    [GlobalSetup]
    public void Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE benchmark_items (id INT, value TEXT);";
            cmd.ExecuteNonQuery();
        }

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
    public async Task DirectDbTransactionBenchmark()
    {
        await using DbTransaction tx = await _connection.BeginTransactionAsync();
        await using (DbCommand cmd = _connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO benchmark_items VALUES (1, 'Direct');";
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    [Benchmark]
    public async Task FrameworkTransactionBenchmark()
    {
        await _manager.ExecuteAsync(async context =>
        {
            await using DbCommand cmd = context.Connection.CreateCommand();
            cmd.Transaction = context.Transaction;
            cmd.CommandText = "INSERT INTO benchmark_items VALUES (2, 'Framework');";
            await cmd.ExecuteNonQueryAsync();
        });
    }

    [Benchmark]
    public async Task FrameworkNestedSavepointBenchmark()
    {
        await _manager.ExecuteAsync(async outerContext =>
        {
            await _manager.ExecuteAsync(async innerContext =>
            {
                await using DbCommand cmd = innerContext.Connection.CreateCommand();
                cmd.Transaction = innerContext.Transaction;
                cmd.CommandText = "INSERT INTO benchmark_items VALUES (3, 'Savepoint');";
                await cmd.ExecuteNonQueryAsync();
            });
        });
    }
}
