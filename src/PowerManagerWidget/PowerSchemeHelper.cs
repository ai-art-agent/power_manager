using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace PowerManagerWidget;

public static class PowerSchemeHelper
{
    private const int MaxPoints = 80;

    public static (string? Min, string? Balanced, string? Max) LoadSchemeGuids()
    {
        var baseDir = AppContext.BaseDirectory;
        var paths = new[]
        {
            Path.Combine(baseDir, "scheme-guids.json"),
            Path.Combine(baseDir, "..", "scheme-guids.json"),
            Path.Combine(baseDir, "..", "..", "..", "..", "scheme-guids.json")
        };
        foreach (var path in paths)
        {
            var full = Path.GetFullPath(path);
            if (!File.Exists(full)) continue;
            try
            {
                var json = File.ReadAllText(full);
                var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                return (
                    r.TryGetProperty("Min", out var m) ? m.GetString() : null,
                    r.TryGetProperty("Balanced", out var b) ? b.GetString() : null,
                    r.TryGetProperty("Max", out var x) ? x.GetString() : null
                );
            }
            catch { /* ignore */ }
        }
        return (null, null, null);
    }

    public static string? GetActiveSchemeGuid()
    {
        try
        {
            var psi = new ProcessStartInfo("powercfg", "/getactivescheme")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return null;
            var line = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(2000);
            // "Power Scheme GUID: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx  (Name)"
            var idx = line.IndexOf("GUID:", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            idx = line.IndexOf(':', idx) + 1;
            var end = line.IndexOf(' ', idx);
            if (end < 0) end = line.Length;
            var guid = line[idx..end].Trim();
            return guid.Length > 30 ? guid : null;
        }
        catch { return null; }
    }

    public static void SetActiveScheme(string guid)
    {
        if (string.IsNullOrWhiteSpace(guid)) return;
        try
        {
            var psi = new ProcessStartInfo("powercfg", $"/setactive {guid}")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(2000);
        }
        catch { }
    }
}

/// <summary>Кольцевой буфер для мини-графика.</summary>
public class RingBuffer
{
    private readonly float[] _data;
    private int _index;
    private int _count;

    public RingBuffer(int capacity)
    {
        _data = new float[capacity];
    }

    public int Capacity => _data.Length;
    public int Count => _count;

    public void Add(float value)
    {
        _data[_index] = value;
        _index = (_index + 1) % _data.Length;
        if (_count < _data.Length) _count++;
    }

    /// <summary>Копирует данные в порядке "старые -> новые" для отрисовки.</summary>
    public void CopyTo(Span<float> target)
    {
        if (_count == 0 || target.IsEmpty) return;
        int n = Math.Min(_count, target.Length);
        int start = _count >= _data.Length ? _index : 0;
        for (int i = 0; i < n; i++)
            target[i] = _data[(start + i) % _data.Length];
    }

    public (float min, float max) GetMinMax()
    {
        if (_count == 0) return (0, 100);
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < _count; i++)
        {
            float v = _data[i];
            if (v < min) min = v;
            if (v > max) max = v;
        }
        if (min > max) return (0, 100);
        float pad = (max - min) * 0.1f;
        if (pad < 1) pad = 1;
        return (min - pad, max + pad);
    }
}
