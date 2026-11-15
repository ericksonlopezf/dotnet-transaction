```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
Intel Xeon 6973P-C 3.87GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host]   : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                            | Mean     | Error    | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------------------- |---------:|---------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| DirectDbTransactionBenchmark      | 4.799 μs | 5.931 μs | 0.3251 μs |  1.00 |    0.08 | 0.0153 | 0.0076 |   1.65 KB |        1.00 |
| FrameworkTransactionBenchmark     |       NA |       NA |        NA |     ? |       ? |     NA |     NA |        NA |           ? |
| FrameworkNestedSavepointBenchmark |       NA |       NA |        NA |     ? |       ? |     NA |     NA |        NA |           ? |

Benchmarks with issues:
  TransactionBenchmarks.FrameworkTransactionBenchmark: ShortRun(IterationCount=3, LaunchCount=1, WarmupCount=3)
  TransactionBenchmarks.FrameworkNestedSavepointBenchmark: ShortRun(IterationCount=3, LaunchCount=1, WarmupCount=3)
