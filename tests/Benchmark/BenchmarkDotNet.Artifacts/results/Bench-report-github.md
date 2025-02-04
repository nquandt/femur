```

BenchmarkDotNet v0.14.0, Windows 10 (10.0.19045.5371/22H2/2022Update)
Intel Core i7-10850H CPU 2.70GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK 9.0.100-preview.6.24328.19
  [Host]     : .NET 8.0.2 (8.0.224.6711), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.2 (8.0.224.6711), X64 RyuJIT AVX2


```
| Method                | Mean     | Error   | StdDev  |
|---------------------- |---------:|--------:|--------:|
| MapEndpoint_Http_Call | 209.0 μs | 3.94 μs | 8.49 μs |
| MapGet_Http_Call      | 211.9 μs | 4.20 μs | 8.76 μs |
