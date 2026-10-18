```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 7763 2.61GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.402
  [Host]   : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                            | Mean     | Error    | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------------------- |---------:|---------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| DirectDbTransactionBenchmark      | 7.468 μs | 2.341 μs | 0.1283 μs |  1.00 |    0.02 | 0.0992 | 0.0916 |   1.65 KB |        1.00 |
| FrameworkTransactionBenchmark     |       NA |       NA |        NA |     ? |       ? |     NA |     NA |        NA |           ? |
| FrameworkNestedSavepointBenchmark |       NA |       NA |        NA |     ? |       ? |     NA |     NA |        NA |           ? |

Benchmarks with issues:
  TransactionBenchmarks.FrameworkTransactionBenchmark: ShortRun(IterationCount=3, LaunchCount=1, WarmupCount=3)
  TransactionBenchmarks.FrameworkNestedSavepointBenchmark: ShortRun(IterationCount=3, LaunchCount=1, WarmupCount=3)
