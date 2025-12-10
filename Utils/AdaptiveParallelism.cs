using System;
using System.Diagnostics;
using System.Threading;

namespace dbmselect.Utils;

public static class AdaptiveParallelism
{
    private static readonly int _maxCores;

    static AdaptiveParallelism()
    {
        _maxCores = Environment.ProcessorCount;
    }

    public static int GetOptimalDegree(double desiredUtilization = 0.8)
    {
        var cpuUsage = GetCpuUsage();
        if (cpuUsage > 70)
        {
            return Math.Max(1, (int)Math.Ceiling(_maxCores * 0.5)); // use only half cores
        }

        return (int)Math.Ceiling(_maxCores * desiredUtilization);
    }

    private static double GetCpuUsage()
    {
        using var process = Process.GetCurrentProcess();

        var startCpuTime = process.TotalProcessorTime;
        var startTime = DateTime.UtcNow;

        Thread.Sleep(200);

        var endCpuTime = process.TotalProcessorTime;
        var endTime = DateTime.UtcNow;

        var cpuUsedMs = (endCpuTime - startCpuTime).TotalMilliseconds;
        var totalMs = (endTime - startTime).TotalMilliseconds * _maxCores;

        return (cpuUsedMs / totalMs) * 100.0;
    }
}