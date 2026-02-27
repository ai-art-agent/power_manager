using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json;
using LibreHardwareMonitor.Hardware;

namespace PowerSchemeTest;

[SupportedOSPlatform("windows")]
static class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        int runsPerScheme = args.Length > 0 && int.TryParse(args[0], out var r) ? r : 5;
        int stressSec = args.Length > 1 && int.TryParse(args[1], out var s) ? s : 30;
        int sampleMs = 2000;

        string jsonPath = Path.Combine(AppContext.BaseDirectory, "scheme-guids.json");
        if (!File.Exists(jsonPath))
            jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "scheme-guids.json");
        if (!File.Exists(jsonPath))
        {
            var root = Directory.GetParent(Directory.GetCurrentDirectory())?.Parent?.FullName;
            if (!string.IsNullOrEmpty(root))
                jsonPath = Path.Combine(root, "scheme-guids.json");
        }
        if (!File.Exists(jsonPath))
        {
            Console.WriteLine("Файл scheme-guids.json не найден. Сначала выполните scripts\\Install-PowerManagerSchemes.ps1");
            return;
        }

        var json = File.ReadAllText(jsonPath);
        var guids = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (guids == null || guids.Count == 0)
        {
            Console.WriteLine("scheme-guids.json пуст или некорректен.");
            return;
        }

        var brightness = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Min"] = 20, ["Balanced"] = 50, ["Max"] = 50
        };

        var computer = new Computer { IsCpuEnabled = true };
        computer.Open();
        var visitor = new UpdateVisitor();

        var csv = new List<string> { "Scheme,Run,MaxTempC,AvgTempC,AvgLoadPct,Samples" };
        var schemes = new[] { "Min", "Balanced", "Max" };

        Console.WriteLine($"=== Тест схем электропитания: по {runsPerScheme} прогонов на схему, нагрузка {stressSec} с ===\n");

        foreach (string schemeName in schemes)
        {
            if (!guids.TryGetValue(schemeName, out var guid) || string.IsNullOrEmpty(guid))
            {
                Console.WriteLine($"  Схема {schemeName} не найдена в JSON, пропуск.");
                continue;
            }

            Console.WriteLine($"  Схема: {schemeName}");
            PowerSchemeApplier.ApplyScheme(guid);
            if (brightness.TryGetValue(schemeName, out int br))
                PowerSchemeApplier.SetBrightness(br);
            Thread.Sleep(5000);

            for (int run = 1; run <= runsPerScheme; run++)
            {
                Console.Write($"    Прогон {run}/{runsPerScheme} ... ");
                StressRunner.Start(cpuThreads: 4, ramMb: 150);
                var (maxT, avgT, avgL) = MetricsCollector.Collect(computer, visitor, sampleMs, stressSec * 1000);
                StressRunner.Stop();
                csv.Add($"{schemeName},{run},{maxT.ToString(CultureInfo.InvariantCulture)},{avgT.ToString(CultureInfo.InvariantCulture)},{avgL.ToString(CultureInfo.InvariantCulture)},{stressSec * 1000 / sampleMs}");
                Console.WriteLine($"MaxTemp={maxT:F1}°C AvgTemp={avgT:F1}°C AvgLoad={avgL:F0}%");
                Thread.Sleep(2000);
            }
            Console.WriteLine();
        }

        string outPath = Path.Combine(Directory.GetCurrentDirectory(), "PowerSchemeTest-Results.csv");
        File.WriteAllLines(outPath, csv, System.Text.Encoding.UTF8);
        Console.WriteLine($"Результаты записаны: {outPath}");
    }
}
