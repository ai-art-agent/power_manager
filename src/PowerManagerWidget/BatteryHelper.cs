using System.Management;
using System.Runtime.InteropServices;

namespace PowerManagerWidget;

public static class BatteryHelper
{
    private const int UnknownTime = 71582788; // Win32_Battery returns this when on AC for EstimatedRunTime

    public static (int? Percent, bool OnBattery, bool Charging, int? MinutesRemaining, int? MinutesToFull) GetStatus()
    {
        int? percent = null;
        bool onBattery = false;
        bool charging = false;
        int? runMins = null;
        int? toFullMins = null;

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
            foreach (ManagementObject mo in searcher.Get())
            {
                using (mo)
                {
                    if (mo["EstimatedChargeRemaining"] != null)
                    {
                        try { percent = Convert.ToInt32(mo["EstimatedChargeRemaining"]); } catch { }
                    }
                    if (mo["BatteryStatus"] != null)
                    {
                        try
                        {
                            var status = Convert.ToUInt32(mo["BatteryStatus"]);
                            onBattery = status == 1 || status == 4 || status == 5;
                            charging = status == 6;
                        }
                        catch { }
                    }
                    if (mo["EstimatedRunTime"] != null)
                    {
                        try
                        {
                            var v = Convert.ToUInt32(mo["EstimatedRunTime"]);
                            if (v != UnknownTime && v < 0x7FFFFFFF && v > 0 && v <= 24 * 60 * 14)
                                runMins = (int)v;
                        }
                        catch { }
                    }
                    if (mo["TimeToFullCharge"] != null)
                    {
                        try
                        {
                            var v = Convert.ToUInt32(mo["TimeToFullCharge"]);
                            if (v < 0x7FFFFFFF && v > 0)
                                toFullMins = (int)(v / 60);
                        }
                        catch { }
                    }
                    break;
                }
            }
        }
        catch { }

        // Запасной вариант: GetSystemPowerStatus (kernel32) — без зависимости от Windows Forms
        if (!percent.HasValue || percent.Value < 0 || percent.Value > 100)
        {
            try
            {
                if (NativePowerStatus.GetSystemPowerStatus(out var status))
                {
                    if (status.BatteryLifePercent >= 0 && status.BatteryLifePercent <= 100)
                        percent = status.BatteryLifePercent;
                    if (status.ACLineStatus == 0)
                        onBattery = true;
                    if (status.BatteryFlag == 8) // 8 = charging
                        charging = true;
                    if (status.BatteryLifeTime > 0 && status.BatteryLifeTime < 0x7FFFFFFF)
                        runMins = status.BatteryLifeTime / 60;
                }
            }
            catch { }
        }

        return (percent, onBattery, charging, runMins, toFullMins);
    }

    private static class NativePowerStatus
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte Reserved1;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll", SetLastError = false)]
        internal static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);
    }
}
