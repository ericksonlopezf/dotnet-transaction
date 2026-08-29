// Copyright © Erickson Lopez. MIT License.
using BenchmarkDotNet.Running;

namespace EricksonLopez.Transaction.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<TransactionBenchmarks>();
    }
}
