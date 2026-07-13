using BenchmarkDotNet.Running;
using HExporter.Benchmarks;

BenchmarkRunner.Run<ExportThroughputBenchmarks>(args: args);
