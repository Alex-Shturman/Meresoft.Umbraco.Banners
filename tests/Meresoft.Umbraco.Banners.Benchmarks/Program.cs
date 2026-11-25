using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Meresoft.Umbraco.Banners.Benchmarks;

// SC-003: one zone adds less than 2 ms at the 95th percentile. The exit code is 1 when a zone render is over it.
const double P95LimitMs = 2.0;

IConfig config = DefaultConfig.Instance.AddColumn(StatisticColumn.P95);
IEnumerable<Summary> summaries = BenchmarkSwitcher.FromAssembly(typeof(ZoneRenderBenchmarks).Assembly).Run(args, config);

int exit = 0;
foreach (Summary summary in summaries)
{
    foreach (BenchmarkReport report in summary.Reports)
    {
        if (!report.Success || report.ResultStatistics is null)
        {
            exit = 1;
            continue;
        }

        double p95Ms = report.ResultStatistics.Percentiles.P95 / 1_000_000.0;
        double meanMs = report.ResultStatistics.Mean / 1_000_000.0;
        bool ok = p95Ms < P95LimitMs;
        Console.WriteLine($"SC-003 {report.BenchmarkCase.Descriptor.WorkloadMethod.Name}: p95 = {p95Ms:F4} ms, mean = {meanMs:F4} ms, limit {P95LimitMs:F1} ms: {(ok ? "PASS" : "FAIL")}");
        if (!ok)
        {
            exit = 1;
        }
    }

    if (summary.HasCriticalValidationErrors)
    {
        exit = 1;
    }
}

return exit;
