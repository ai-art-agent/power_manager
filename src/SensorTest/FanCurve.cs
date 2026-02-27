namespace SensorTest;

/// <summary>
/// Кривая вентилятора: по максимальной температуре возвращает рекомендуемую мощность (%)
/// и ступень RPM для ноутбуков с дискретными ступенями (например Dell Latitude: 0, 2400, 5300 RPM).
/// Обороты не считываются — только расчёт по температуре.
/// </summary>
public static class FanCurve
{
    /// <summary>Ступени RPM, как в HWiNFO для Dell (0, 2400, 5300).</summary>
    public static readonly int[] RpmSteps = { 0, 2400, 5300 };

    /// <summary>
    /// Пороги температуры °C для переключения ступеней (как в вашей таблице HWiNFO):
    /// до 20°C → 0 RPM, 21–60°C → 2400 RPM, 61°C и выше → 5300 RPM.
    /// </summary>
    public static (int StepIndex, int Rpm, double Percent) GetFanLevel(double maxTempCelsius)
    {
        if (maxTempCelsius <= 20)
            return (0, RpmSteps[0], 0);
        if (maxTempCelsius <= 60)
            return (1, RpmSteps[1], 100.0 * RpmSteps[1] / RpmSteps[2]); // ~45%
        return (2, RpmSteps[2], 100);
    }

    /// <summary>Текстовое описание ступени для вывода.</summary>
    public static string GetStepDescription(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= RpmSteps.Length)
            return "?";
        return RpmSteps[stepIndex] == 0 ? "0 RPM (выкл.)" : $"{RpmSteps[stepIndex]} RPM";
    }
}
