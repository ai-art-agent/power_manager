using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using LibreHardwareMonitor.Hardware;

namespace PowerSchemeTest;

internal class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer) => computer.Traverse(this);
    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (IHardware sub in hardware.SubHardware)
            sub.Accept(this);
    }
    public void VisitSensor(ISensor sensor) { }
    public void VisitParameter(IParameter parameter) { }
}

[SupportedOSPlatform("windows")]
internal static class PowerSchemeApplier
{
    public static void ApplyScheme(string schemeGuid)
    {
        var start = new ProcessStartInfo
        {
            FileName = "powercfg.exe",
            Arguments = "/setactive " + schemeGuid,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(start);
        p?.WaitForExit(5000);
    }

    public static void SetBrightness(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
            foreach (ManagementObject obj in searcher.Get())
                obj.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)percent });
        }
        catch { /* ignore */ }
    }
}

internal static class StressRunner
{
    private static volatile bool _running;
    private static readonly List<byte[]> _hold = new();

    public static void Start(int cpuThreads = 4, int ramMb = 200)
    {
        _running = true;
        _hold.Clear();
        for (int t = 0; t < cpuThreads; t++)
        {
            var th = new Thread(() =>
            {
                while (_running)
                    _ = 1 + 1;
            });
            th.IsBackground = true;
            th.Start();
        }
        var alloc = new Thread(() =>
        {
            try
            {
                while (_running && ramMb > 0)
                {
                    _hold.Add(new byte[1024 * 1024]);
                    if (_hold.Count >= ramMb) break;
                }
                while (_running)
                    Thread.Sleep(100);
            }
            finally
            {
                _hold.Clear();
            }
        });
        alloc.IsBackground = true;
        alloc.Start();
    }

    public static void Stop()
    {
        _running = false;
        _hold.Clear();
        Thread.Sleep(500);
    }
}

internal static class MetricsCollector
{
    public static (float MaxTempC, float AvgTempC, float AvgLoadPct) Collect(Computer computer, UpdateVisitor visitor, int sampleIntervalMs, int durationMs)
    {
        var temps = new List<float>();
        var loads = new List<float>();
        int elapsed = 0;
        while (elapsed < durationMs)
        {
            computer.Accept(visitor);
            float maxTemp = 0, loadTotal = 0;
            int loadCount = 0;
            foreach (var hw in computer.Hardware)
            {
                hw.Update();
                if (hw.HardwareType != HardwareType.Cpu) continue;
                foreach (var s in hw.Sensors)
                {
                    if (s.SensorType == SensorType.Temperature && s.Value.HasValue && float.IsNormal(s.Value.Value))
                        maxTemp = Math.Max(maxTemp, s.Value.Value);
                    if (s.SensorType == SensorType.Load && s.Value.HasValue && float.IsNormal(s.Value.Value))
                    { loadTotal += s.Value.Value; loadCount++; }
                }
            }
            if (maxTemp > 0) temps.Add(maxTemp);
            if (loadCount > 0) loads.Add(loadTotal / loadCount);
            Thread.Sleep(sampleIntervalMs);
            elapsed += sampleIntervalMs;
        }
        float maxT = temps.Count > 0 ? temps.Max() : 0;
        float avgT = temps.Count > 0 ? (float)temps.Average() : 0;
        float avgL = loads.Count > 0 ? (float)loads.Average() : 0;
        return (maxT, avgT, avgL);
    }
}
