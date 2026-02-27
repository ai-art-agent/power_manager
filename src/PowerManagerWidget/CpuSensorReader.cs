using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace PowerManagerWidget;

/// <summary>
/// Чтение частоты CPU, загрузки и максимальной температуры по всем датчикам (самая горячая точка).
/// </summary>
public sealed class CpuSensorReader
{
    private readonly Computer _computer;
    private readonly IVisitor _visitor;
    private readonly List<(IHardware Hw, ISensor Sensor)> _cpuTempSensors = new();
    private readonly List<(IHardware Hw, ISensor Sensor)> _cpuLoadSensors = new();
    private readonly List<(IHardware Hw, ISensor Sensor)> _cpuClockSensors = new();

    public CpuSensorReader()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = false,
            IsMemoryEnabled = false,
            IsMotherboardEnabled = false,
            IsStorageEnabled = false,
            IsNetworkEnabled = false
        };
        _visitor = new UpdateVisitor();
    }

    public void Open()
    {
        _computer.Open();
        CollectCpuSensors(_computer.Hardware.ToList());
    }

    public void Close()
    {
        _computer.Close();
    }

    private void CollectCpuSensors(IReadOnlyList<IHardware> hardwareList)
    {
        foreach (var hw in hardwareList)
        {
            hw.Accept(_visitor);
            bool isCpu = hw.HardwareType == HardwareType.Cpu ||
                         (hw.Name?.Contains("CPU", StringComparison.OrdinalIgnoreCase) ?? false);

            foreach (var sensor in hw.Sensors)
            {
                if (!isCpu) continue;
                if (sensor.SensorType == SensorType.Temperature) _cpuTempSensors.Add((hw, sensor));
                if (sensor.SensorType == SensorType.Load) _cpuLoadSensors.Add((hw, sensor));
                if (sensor.SensorType == SensorType.Clock) _cpuClockSensors.Add((hw, sensor));
            }

            var sub = hw.SubHardware;
            if (sub != null && sub.Any())
                CollectCpuSensors(sub.ToList());
        }
    }

    /// <summary>Обновить все датчики и вернуть текущие значения.</summary>
    public (float? LoadPercent, float? FrequencyMhz, float? MaxTempCelsius) Update()
    {
        _computer.Accept(_visitor);

        float? load = null;
        foreach (var (hw, sensor) in _cpuLoadSensors)
        {
            hw.Update();
            if (sensor.Value.HasValue && float.IsNormal(sensor.Value.Value))
            {
                if (!load.HasValue || sensor.Value.Value > load.Value)
                    load = sensor.Value.Value;
            }
        }
        // Если несколько датчиков нагрузки (пакет/ядра), берём макс или первый "Total"
        if (_cpuLoadSensors.Count > 1)
        {
            var total = _cpuLoadSensors.FirstOrDefault(s => s.Sensor.Name?.Contains("Total", StringComparison.OrdinalIgnoreCase) == true);
            if (total.Sensor != null)
            {
                total.Hw.Update();
                if (total.Sensor.Value.HasValue) load = total.Sensor.Value.Value;
            }
        }

        float? freqMhz = null;
        foreach (var (hw, sensor) in _cpuClockSensors)
        {
            hw.Update();
            if (sensor.Value.HasValue && float.IsNormal(sensor.Value.Value))
            {
                float v = sensor.Value.Value;
                if (v > 10000) v /= 1000f; // если в кГц
                if (!freqMhz.HasValue || v > freqMhz.Value) freqMhz = v;
            }
        }

        float? maxTemp = null;
        foreach (var (hw, sensor) in _cpuTempSensors)
        {
            hw.Update();
            if (sensor.Value.HasValue && float.IsNormal(sensor.Value.Value))
            {
                if (!maxTemp.HasValue || sensor.Value.Value > maxTemp.Value)
                    maxTemp = sensor.Value.Value;
            }
        }

        return (load, freqMhz, maxTemp);
    }
}
