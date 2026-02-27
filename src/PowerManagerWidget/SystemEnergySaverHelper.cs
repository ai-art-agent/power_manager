using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PowerManagerWidget;

/// <summary>
/// Включение/выключение системного режима «Энергосбережение» (Battery saver) через powercfg.
/// </summary>
public static class SystemEnergySaverHelper
{
    /// <summary>Включить энергосбережение (порог 100% при питании от батареи).</summary>
    public static void Enable()
    {
        try
        {
            RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTTHRESHOLD 100");
            RunPowerCfg("/setactive SCHEME_CURRENT");
        }
        catch { }
    }

    /// <summary>Выключить авто-включение (порог 20%).</summary>
    public static void Disable()
    {
        try
        {
            RunPowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTTHRESHOLD 20");
            RunPowerCfg("/setactive SCHEME_CURRENT");
        }
        catch { }
    }

    /// <summary>Порог включения по батарее 100% = режим «включён».</summary>
    public static bool IsEnabled()
    {
        try
        {
            var outText = RunPowerCfgCapture("/query SCHEME_CURRENT SUB_ENERGYSAVER");
            var m = Regex.Match(outText ?? "", @"Current DC Power Setting Index:\s*0x([0-9A-Fa-f]+)");
            if (m.Success && int.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out int idx))
                return idx >= 100;
        }
        catch { }
        return false;
    }

    private static void RunPowerCfg(string args)
    {
        using var p = new Process
        {
            StartInfo = new ProcessStartInfo("powercfg", args)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        p.Start();
        p.WaitForExit(3000);
    }

    private static string? RunPowerCfgCapture(string args)
    {
        using var p = new Process
        {
            StartInfo = new ProcessStartInfo("powercfg", args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            }
        };
        p.Start();
        var outText = p.StandardOutput.ReadToEnd();
        p.WaitForExit(3000);
        return outText;
    }
}
