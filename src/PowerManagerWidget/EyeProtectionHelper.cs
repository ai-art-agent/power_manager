using System.Runtime.InteropServices;

namespace PowerManagerWidget;

/// <summary>
/// Интенсивность режима защиты глаз (тёплый/жёлтый оттенок экрана) через гамма-рампу.
/// Ползунок 0% — без изменений, 100% — максимальное снижение синего.
/// </summary>
public static class EyeProtectionHelper
{
    private const int RampSize = 256;
    private static ushort[]? _baseR;
    private static ushort[]? _baseG;
    private static ushort[]? _baseB;
    private static bool _baseSaved;
    private static int _currentPercent = -1;

    public static void SetIntensity(int percent)
    {
        if (percent < 0) percent = 0;
        if (percent > 100) percent = 100;
        _currentPercent = percent;

        try
        {
            EnsureBaseRamp();
            if (_baseR == null || _baseG == null || _baseB == null) return;

            var r = new ushort[RampSize];
            var g = new ushort[RampSize];
            var b = new ushort[RampSize];

            double k = percent / 100.0; // 0 = нет эффекта, 1 = макс. желтизна
            double gScale = 1.0 - 0.25 * k; // зелёный чуть снижаем
            double bScale = 1.0 - k;        // синий сильно снижаем

            for (int i = 0; i < RampSize; i++)
            {
                r[i] = _baseR[i];
                g[i] = (ushort)Math.Clamp(_baseG[i] * gScale, 0, 65535);
                b[i] = (ushort)Math.Clamp(_baseB[i] * bScale, 0, 65535);
            }

            SetRamp(r, g, b);
        }
        catch { }
    }

    public static void Restore()
    {
        if (!_baseSaved || _baseR == null || _baseG == null || _baseB == null) return;
        try { SetRamp(_baseR, _baseG, _baseB); } catch { }
        _currentPercent = 0;
    }

    /// <summary>Текущая установленная интенсивность (0–100) или null, если не инициализировано.</summary>
    public static int? GetCurrentIntensity() => _baseSaved ? _currentPercent : null;

    private static void EnsureBaseRamp()
    {
        if (_baseSaved) return;
        try
        {
            if (GetRamp(out var br, out var bg, out var bb))
            {
                _baseR = br;
                _baseG = bg;
                _baseB = bb;
                _baseSaved = true;
            }
            else
            {
                // Линейная рампа по умолчанию
                _baseR = new ushort[RampSize];
                _baseG = new ushort[RampSize];
                _baseB = new ushort[RampSize];
                for (int i = 0; i < RampSize; i++)
                {
                    ushort v = (ushort)((i << 8) | i);
                    _baseR[i] = _baseG[i] = _baseB[i] = v;
                }
                _baseSaved = true;
            }
        }
        catch
        {
            _baseR = new ushort[RampSize];
            _baseG = new ushort[RampSize];
            _baseB = new ushort[RampSize];
            for (int i = 0; i < RampSize; i++)
            {
                ushort v = (ushort)((i << 8) | i);
                _baseR[i] = _baseG[i] = _baseB[i] = v;
            }
            _baseSaved = true;
        }
    }

    private static void SetRamp(ushort[] r, ushort[] g, ushort[] b)
    {
        var hdc = GetDC(IntPtr.Zero);
        if (hdc == IntPtr.Zero) return;
        try
        {
            var ramp = new GammaRamp { Red = r, Green = g, Blue = b };
            SetDeviceGammaRamp(hdc, ref ramp);
        }
        finally { ReleaseDC(IntPtr.Zero, hdc); }
    }

    private static bool GetRamp(out ushort[] r, out ushort[] g, out ushort[] b)
    {
        r = new ushort[RampSize];
        g = new ushort[RampSize];
        b = new ushort[RampSize];
        var hdc = GetDC(IntPtr.Zero);
        if (hdc == IntPtr.Zero) return false;
        try
        {
            var ramp = new GammaRamp { Red = r, Green = g, Blue = b };
            return GetDeviceGammaRamp(hdc, ref ramp);
        }
        finally { ReleaseDC(IntPtr.Zero, hdc); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct GammaRamp
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Red;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Green;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Blue;
    }

    [DllImport("gdi32.dll")]
    private static extern bool GetDeviceGammaRamp(IntPtr hdc, ref GammaRamp ramp);

    [DllImport("gdi32.dll")]
    private static extern bool SetDeviceGammaRamp(IntPtr hdc, ref GammaRamp ramp);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
}
