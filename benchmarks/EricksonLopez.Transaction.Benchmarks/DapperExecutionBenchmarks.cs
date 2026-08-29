// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Dapper;
using EricksonLopez.Transaction;
using EricksonLopez.Transaction.Dapper;
using Microsoft.Data.Sqlite;

namespace EricksonLopez.Transaction.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class DapperExecutionBenchmarks : IDisposable
{
    private SqliteConnection _connection = null!;
    private ITransactionManager _manager = null!;

    [GlobalSetup]
    public void Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _connection.Execute("CREATE TABLE dapper_benchmarks (id INT, value TEXT);");
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
    public async Task DirectDapperInsert()
    {
        await _connection.ExecuteAsync("INSERT INTO dapper_benchmarks VALUES (@Id, @Value);", new { Id = 1, Value = "Direct" });
    }

    [Benchmark]
    public async Task TransactionContextDapperInsert()
    {
        await _manager.ExecuteAsync(async ctx =>
        {
            await ctx.ExecuteAsync("INSERT INTO dapper_benchmarks VALUES (@Id, @Value);", new { Id = 2, Value = "Context" });
        });
    }
}
