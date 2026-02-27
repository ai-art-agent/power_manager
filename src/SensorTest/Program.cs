using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace SensorTest;

static class Program
{
    [SupportedOSPlatform("windows")]
    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== ТЕСТ ДАТЧИКОВ: КУЛЕРЫ И ТЕМПЕРАТУРА ПРОЦЕССОРА ===\n");

        var computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsStorageEnabled = true,
            IsNetworkEnabled = false
        };

        try
        {
            computer.Open();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка открытия доступа к железу (попробуйте запуск от администратора):");
            Console.WriteLine(ex.Message);
            return;
        }

        var visitor = new UpdateVisitor();
        computer.Accept(visitor);

        // Собираем все датчики кулеров (Fan) и температуры CPU по всему дереву
        var fanSensors = new List<(IHardware Hardware, ISensor Sensor)>();
        var cpuTempSensors = new List<(IHardware Hardware, ISensor Sensor)>();

        CollectSensors(computer.Hardware.ToList(), visitor, fanSensors, cpuTempSensors);

        // --- Поочерёдный тест датчиков кулеров ---
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  ДАТЧИКИ КУЛЕРОВ (Fan / RPM)                                 ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝\n");

        if (fanSensors.Count == 0)
        {
            Console.WriteLine("  LibreHardwareMonitor не обнаружил датчиков кулеров.");
            Console.WriteLine("  (На многих платах датчики вентиляторов не видны из-за перехода на драйвер PawnIO.)\n");
            Console.WriteLine("  Для Dell Latitude: обороты можно получить через HWiNFO (см. ниже).\n");
            DumpFullHardwareTree(computer, visitor);
            Console.WriteLine();
        }
        else
        {
            for (int i = 0; i < fanSensors.Count; i++)
            {
                var (hardware, sensor) = fanSensors[i];
                TestOneSensor(
                    index: i + 1,
                    total: fanSensors.Count,
                    hardwareName: hardware.Name,
                    sensorName: sensor.Name,
                    sensorType: "Fan (RPM)",
                    unit: "об/мин",
                    getValue: () =>
                    {
                        hardware.Update();
                        return sensor.Value;
                    }
                );
            }
        }

        // Альтернатива: обороты из HWiNFO (если запущен с Shared Memory)
        TryPrintHwiNfoFans();

        // Рекомендуемая мощность вентилятора по макс. температуре (без считывания оборотов)
        PrintFanCurveRecommendation(computer, visitor);

        // --- Поочерёдный тест датчиков температуры процессора ---
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  ДАТЧИКИ ТЕМПЕРАТУРЫ ПРОЦЕССОРА                             ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝\n");

        if (cpuTempSensors.Count == 0)
        {
            Console.WriteLine("  Нет доступных датчиков температуры CPU.\n");
        }
        else
        {
            for (int i = 0; i < cpuTempSensors.Count; i++)
            {
                var (hardware, sensor) = cpuTempSensors[i];
                TestOneSensor(
                    index: i + 1,
                    total: cpuTempSensors.Count,
                    hardwareName: hardware.Name,
                    sensorName: sensor.Name,
                    sensorType: "Temperature",
                    unit: "°C",
                    getValue: () =>
                    {
                        hardware.Update();
                        return sensor.Value;
                    }
                );
            }
        }

        Console.WriteLine("\n=== ТЕСТ ЗАВЕРШЁН ===");
    }

    static void CollectSensors(
        IReadOnlyList<IHardware> hardwareList,
        UpdateVisitor visitor,
        List<(IHardware Hardware, ISensor Sensor)> fanSensors,
        List<(IHardware Hardware, ISensor Sensor)> cpuTempSensors)
    {
        foreach (var hw in hardwareList)
        {
            hw.Accept(visitor);

            bool isCpu = hw.HardwareType == HardwareType.Cpu ||
                         hw.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase);

            foreach (var sensor in hw.Sensors)
            {
                if (sensor.SensorType == SensorType.Fan)
                    fanSensors.Add((hw, sensor));
                if (sensor.SensorType == SensorType.Temperature && isCpu)
                    cpuTempSensors.Add((hw, sensor));
            }

            var subHardware = hw.SubHardware;
            if (subHardware != null && subHardware.Count() > 0)
                CollectSensors(subHardware!.ToList(), visitor, fanSensors, cpuTempSensors);
        }
    }

    /// <summary>Собирает все датчики температуры по всему дереву (CPU, GPU и т.д.).</summary>
    static void CollectAllTempSensors(
        IReadOnlyList<IHardware> hardwareList,
        UpdateVisitor visitor,
        List<(IHardware Hardware, ISensor Sensor)> outTempSensors)
    {
        foreach (var hw in hardwareList)
        {
            hw.Accept(visitor);
            foreach (var sensor in hw.Sensors)
                if (sensor.SensorType == SensorType.Temperature)
                    outTempSensors.Add((hw, sensor));
            var sub = hw.SubHardware;
            if (sub != null && sub.Count() > 0)
                CollectAllTempSensors(sub!.ToList(), visitor, outTempSensors);
        }
    }

    /// <summary>Рекомендуемая мощность вентилятора по максимальной температуре среди всех датчиков. Обороты не считываются.</summary>
    static void PrintFanCurveRecommendation(Computer computer, UpdateVisitor visitor)
    {
        var allTemps = new List<(IHardware Hardware, ISensor Sensor)>();
        CollectAllTempSensors(computer.Hardware.ToList(), visitor, allTemps);

        float? maxTemp = null;
        foreach (var (hw, sensor) in allTemps)
        {
            hw.Update();
            if (sensor.Value.HasValue && float.IsNormal(sensor.Value.Value))
            {
                if (!maxTemp.HasValue || sensor.Value.Value > maxTemp.Value)
                    maxTemp = sensor.Value.Value;
            }
        }

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  РЕКОМЕНДУЕМАЯ МОЩНОСТЬ ВЕНТИЛЯТОРА (по макс. температуре)   ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝\n");

        if (!maxTemp.HasValue)
        {
            Console.WriteLine("  Нет доступных датчиков температуры для расчёта.\n");
            return;
        }

        var (stepIndex, _, percent) = FanCurve.GetFanLevel(maxTemp.Value);
        Console.WriteLine($"  Максимальная температура среди датчиков: {maxTemp.Value:F1} °C");
        Console.WriteLine($"  Рекомендуемая мощность вентилятора:        {percent:F0}%");
        Console.WriteLine($"  Ступень (как в HWiNFO):                   {FanCurve.GetStepDescription(stepIndex)}");
        Console.WriteLine();
        Console.WriteLine("  Кривая: ≤20°C → 0 RPM; 21–60°C → 2400 RPM; ≥61°C → 5300 RPM.");
        Console.WriteLine("  Управление вентилятором приложение не выполняет — только расчёт.");
        Console.WriteLine("  Чтобы применить эту ступень, настройте таблицу в HWiNFO (Fan Control Look-up Table)\n");
    }

    static void TestOneSensor(
        int index,
        int total,
        string hardwareName,
        string sensorName,
        string sensorType,
        string unit,
        Func<float?> getValue)
    {
        Console.WriteLine($"  [{index}/{total}] {sensorType}");
        Console.WriteLine($"      Устройство: {hardwareName}");
        Console.WriteLine($"      Датчик:    {sensorName}");
        Console.Write("      Проверка:  ");

        float? value = null;
        try
        {
            value = getValue();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Ошибка чтения — {ex.Message}");
            Console.ResetColor();
        }

        if (value.HasValue)
        {
            string valueStr = value.Value.ToString("F1");
            Console.Write($"Значение = {valueStr} {unit}");
            if (float.IsNormal(value.Value) || value.Value == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  [OK]");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  [нет данных или N/A]");
                Console.ResetColor();
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Выводит полное дерево железа и все датчики/контролы — для диагностики, почему нет датчиков вентиляторов.
    /// </summary>
    static void DumpFullHardwareTree(Computer computer, UpdateVisitor visitor)
    {
        Console.WriteLine("  --- Дерево устройств LibreHardwareMonitor (диагностика) ---");
        DumpHardwareRecursive(computer.Hardware.ToList(), visitor, 0);
    }

    static void DumpHardwareRecursive(IEnumerable<IHardware> hardwareList, UpdateVisitor visitor, int indent)
    {
        var prefix = new string(' ', indent);
        foreach (var hw in hardwareList)
        {
            hw.Accept(visitor);
            Console.WriteLine($"{prefix}[{hw.HardwareType}] {hw.Name}");
            foreach (var s in hw.Sensors)
                Console.WriteLine($"{prefix}  Sensor: {s.SensorType} — {s.Name} = {s.Value?.ToString("F1") ?? "N/A"}");
            var sub = hw.SubHardware;
            if (sub != null && sub.Count() > 0)
                DumpHardwareRecursive(sub.ToList(), visitor, indent + 2);
        }
    }

    [SupportedOSPlatform("windows")]
    static void TryPrintHwiNfoFans()
    {
        var fans = HwiNfoReader.TryReadFanRpm();
        if (fans.Count == 0)
        {
            Console.WriteLine("  (Обороты из HWiNFO не получены. Запустите HWiNFO с включённой опцией Shared Memory Support,\n   затем перезапустите тест — здесь появятся датчики вентиляторов.)\n");
            return;
        }
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  ОБОРОТЫ ВЕНТИЛЯТОРОВ ИЗ HWiNFO (Shared Memory)             ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine("  (Запустите HWiNFO с включённой опцией Shared Memory Support.)\n");
        for (int i = 0; i < fans.Count; i++)
        {
            var f = fans[i];
            Console.WriteLine($"  [{i + 1}/{fans.Count}] {f.Label}");
            Console.WriteLine($"      Значение: {f.ValueRpm:F0} {f.Unit}  (мин: {f.Min:F0}, макс: {f.Max:F0})");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("      [OK]");
            Console.ResetColor();
            Console.WriteLine();
        }
    }
}
