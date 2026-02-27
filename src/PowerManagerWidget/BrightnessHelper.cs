using System.Management;

namespace PowerManagerWidget;

public static class BrightnessHelper
{
    public static byte? GetBrightness()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightness");
            foreach (var obj in searcher.Get())
            {
                using (obj)
                {
                    var current = obj["CurrentBrightness"];
                    if (current != null)
                        return (byte)current;
                }
            }
        }
        catch { }
        return null;
    }

    public static void SetBrightness(byte level)
    {
        if (level > 100) level = 100;
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
            foreach (ManagementObject mo in searcher.Get())
            {
                using (mo)
                    mo.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, level });
            }
        }
        catch { }
    }
}
