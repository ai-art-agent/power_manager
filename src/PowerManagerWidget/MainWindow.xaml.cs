using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PowerManagerWidget;

public partial class MainWindow : Window
{
    private const int GraphPoints = 80;
    private readonly CpuSensorReader _sensorReader;
    private readonly RingBuffer _freqBuffer = new(GraphPoints);
    private readonly RingBuffer _loadBuffer = new(GraphPoints);
    private readonly RingBuffer _tempBuffer = new(GraphPoints);
    private string? _guidMin, _guidBal, _guidMax;
    private bool _updatingSchemeUi;
    private System.Threading.Timer? _sensorTimer;
    private readonly Polyline _polyFreq = new(), _polyLoad = new(), _polyTemp = new();
    private bool _updatingBrightnessSlider;
    private bool _updatingVolumeSlider;
    private bool _autoMode;
    private DispatcherTimer? _blinkTimer;
    private string _recommendedScheme = "Balanced";
    private int _updateIntervalMs = 1500;
    private int _autoCheckIntervalMs = 3000;
    private long _lastAutoCheckMs;
    private int _tickCount;
    private bool _uiUpdatePending;

    public MainWindow()
    {
        InitializeComponent();
        _sensorReader = new CpuSensorReader();
        try { _sensorReader.Open(); } catch { }

        _polyFreq.Stroke = (Brush)FindResource("GraphCpu");
        _polyFreq.StrokeThickness = 1.5;
        _polyFreq.StrokeLineJoin = PenLineJoin.Round;
        _polyLoad.Stroke = (Brush)FindResource("GraphLoad");
        _polyLoad.StrokeThickness = 1.5;
        _polyLoad.StrokeLineJoin = PenLineJoin.Round;
        _polyTemp.Stroke = (Brush)FindResource("GraphTemp");
        _polyTemp.StrokeThickness = 1.5;
        _polyTemp.StrokeLineJoin = PenLineJoin.Round;

        LoadSchemeGuids();
        RefreshActiveScheme();

        _updateIntervalMs = 1500;
        InitUpdateIntervalCombo();
        InitAutoCheckCombo();
        RestartSensorTimer(_updateIntervalMs);
    }

    private void InitAutoCheckCombo()
    {
        if (CbAutoCheckInterval == null) return;
        CbAutoCheckInterval.Items.Clear();
        CbAutoCheckInterval.Items.Add("1 с");
        CbAutoCheckInterval.Items.Add("2 с");
        CbAutoCheckInterval.Items.Add("3 с");
        CbAutoCheckInterval.Items.Add("5 с");
        CbAutoCheckInterval.Items.Add("10 с");
        CbAutoCheckInterval.SelectedIndex = 1; // 2 с по умолчанию
    }

    private void InitUpdateIntervalCombo()
    {
        if (CbUpdateInterval == null) return;
        CbUpdateInterval.Items.Clear();
        CbUpdateInterval.Items.Add("0.5 с");
        CbUpdateInterval.Items.Add("1 с");
        CbUpdateInterval.Items.Add("2 с");
        CbUpdateInterval.Items.Add("5 с");
        CbUpdateInterval.SelectedIndex = 1;
    }

    private void RestartSensorTimer(int intervalMs)
    {
        _sensorTimer?.Dispose();
        _sensorTimer = new System.Threading.Timer(_ =>
        {
            try
            {
                if (_uiUpdatePending) return;
                var (load, freqMhz, maxTemp) = _sensorReader.Update();
                var battery = BatteryHelper.GetStatus();
                _uiUpdatePending = true;
                Dispatcher.BeginInvoke(() =>
                {
                    _uiUpdatePending = false;
                    UpdateUi(load, freqMhz, maxTemp, battery);
                }, DispatcherPriority.Background);
            }
            catch { }
        }, null, 0, intervalMs);
    }

    private void UpdateUi(float? load, float? freqMhz, float? maxTemp,
        (int? Percent, bool OnBattery, bool Charging, int? MinutesRemaining, int? MinutesToFull) battery)
    {
        if (load.HasValue)
        {
            _loadBuffer.Add(load.Value);
            TbLoad.Text = $"{load.Value:F0}%";
        }
        else
            TbLoad.Text = "--%";

        string freqStr = freqMhz.HasValue
            ? (freqMhz.Value >= 500 ? $"{freqMhz.Value / 1000f:F2}" : $"{freqMhz.Value:F2}") + " ГГц"
            : "-- ГГц";
        TbCpu.Text = freqStr;

        if (freqMhz.HasValue) _freqBuffer.Add(freqMhz.Value);
        if (maxTemp.HasValue)
        {
            _tempBuffer.Add(maxTemp.Value);
            TbTemp.Text = $"{maxTemp.Value:F0} °C";
        }
        else
            TbTemp.Text = "-- °C";

        var (fMin, fMax) = _freqBuffer.GetMinMax();
        TbFreqScale.Text = _freqBuffer.Count >= 2 ? $"{fMin / 1000f:F1}–{fMax / 1000f:F1} ГГц" : "";
        var (tMin, tMax) = _tempBuffer.GetMinMax();
        TbTempScale.Text = _tempBuffer.Count >= 2 ? $"{tMin:F0}–{tMax:F0} °C" : "";

        UpdateBatteryTime(battery);
        UpdateRecommendedIndicator(load, maxTemp, battery);
        if (_autoMode)
        {
            long now = Environment.TickCount64;
            if (now - _lastAutoCheckMs >= _autoCheckIntervalMs)
            {
                _lastAutoCheckMs = now;
                ApplyRecommendedScheme(GetRecommendedScheme(load, maxTemp, battery));
            }
        }

        _tickCount++;
        bool drawGraphs = (_tickCount % 2 == 0);
        if (drawGraphs)
        {
            UpdatePolyline(CanvasFreq, _polyFreq, _freqBuffer);
            UpdatePolyline(CanvasLoad, _polyLoad, _loadBuffer);
            UpdateTempPolylineColored(CanvasTemp, _tempBuffer);
        }
    }

    private static System.Windows.Media.Brush TempToBrush(Brush green, Brush yellow, Brush orange, Brush red, float temp)
    {
        if (temp <= 55) return green;
        if (temp <= 70) return yellow;
        if (temp <= 85) return orange;
        return red;
    }

    private void UpdateTempPolylineColored(System.Windows.Controls.Canvas canvas, RingBuffer buffer)
    {
        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        canvas.Children.Clear();
        var gridBrush = (Brush)FindResource("GridLine");
        var green = (Brush)FindResource("GraphTempGreen");
        var yellow = (Brush)FindResource("GraphTempYellow");
        var orange = (Brush)FindResource("GraphTempOrange");
        var red = (Brush)FindResource("GraphTempRed");

        if (buffer.Count >= 2)
        {
            var (min, max) = buffer.GetMinMax();
            float range = max - min;
            if (range < 0.001f) range = 1;

            DrawGrid(canvas, w, h, gridBrush);

            int n = Math.Min(buffer.Count, GraphPoints);
            var pts = new float[n];
            buffer.CopyTo(pts.AsSpan());
            for (int i = 0; i < n - 1; i++)
            {
                float x0 = (float)(w * (i / (double)(n - 1)));
                float x1 = (float)(w * ((i + 1) / (double)(n - 1)));
                float y0 = (float)(h - (pts[i] - min) / range * h);
                float y1 = (float)(h - (pts[i + 1] - min) / range * h);
                float segTemp = Math.Max(pts[i], pts[i + 1]);
                var line = new Line
                {
                    X1 = x0, Y1 = y0, X2 = x1, Y2 = y1,
                    Stroke = TempToBrush(green, yellow, orange, red, segTemp),
                    StrokeThickness = 1.5,
                    StrokeLineJoin = PenLineJoin.Round
                };
                canvas.Children.Add(line);
            }
        }
    }

    private void UpdatePolyline(System.Windows.Controls.Canvas canvas, Polyline poly, RingBuffer buffer)
    {
        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        canvas.Children.Clear();
        var gridBrush = (Brush)FindResource("GridLine");

        if (buffer.Count >= 2)
        {
            var (min, max) = buffer.GetMinMax();
            float range = max - min;
            if (range < 0.001f) range = 1;

            DrawGrid(canvas, w, h, gridBrush);

            int n = Math.Min(buffer.Count, GraphPoints);
            var pts = new float[n];
            buffer.CopyTo(pts.AsSpan());
            var points = new PointCollection(n);
            for (int i = 0; i < n; i++)
            {
                float x = (float)(w * (i / (double)(n - 1)));
                float y = (float)(h - (pts[i] - min) / range * h);
                points.Add(new System.Windows.Point(x, y));
            }
            poly.Points = points;
            canvas.Children.Add(poly);
        }
    }

    private static void DrawGrid(System.Windows.Controls.Canvas canvas, double w, double h, Brush brush)
    {
        const int hLines = 4;
        const int vLines = 5;
        for (int i = 1; i <= hLines; i++)
        {
            double y = h * i / (hLines + 1);
            var line = new Line { X1 = 0, Y1 = y, X2 = w, Y2 = y, Stroke = brush, StrokeThickness = 1 };
            canvas.Children.Add(line);
        }
        for (int i = 1; i <= vLines; i++)
        {
            double x = w * i / (vLines + 1);
            var line = new Line { X1 = x, Y1 = 0, X2 = x, Y2 = h, Stroke = brush, StrokeThickness = 1 };
            canvas.Children.Add(line);
        }
    }

    private void LoadSchemeGuids()
    {
        var (min, bal, max) = PowerSchemeHelper.LoadSchemeGuids();
        _guidMin = min;
        _guidBal = bal;
        _guidMax = max;
    }

    private void RefreshActiveScheme()
    {
        _updatingSchemeUi = true;
        try
        {
            var active = PowerSchemeHelper.GetActiveSchemeGuid();
            if (active == null) return;
            if (string.Equals(active, _guidMin, StringComparison.OrdinalIgnoreCase))
            {
                RbMin.IsChecked = true;
                ChkEnergySaver.IsChecked = true;
            }
            else
            {
                if (string.Equals(active, _guidBal, StringComparison.OrdinalIgnoreCase)) RbBalanced.IsChecked = true;
                else if (string.Equals(active, _guidMax, StringComparison.OrdinalIgnoreCase)) RbMax.IsChecked = true;
                ChkEnergySaver.IsChecked = false;
            }
        }
        finally { _updatingSchemeUi = false; }
    }

    private void Scheme_Checked(object sender, RoutedEventArgs e)
    {
        if (_updatingSchemeUi) return;
        if (sender is not System.Windows.Controls.Primitives.ToggleButton btn || btn.Tag is not string tag) return;
        string? guid = tag switch { "Min" => _guidMin, "Balanced" => _guidBal, "Max" => _guidMax, _ => null };
        if (!string.IsNullOrEmpty(guid))
        {
            PowerSchemeHelper.SetActiveScheme(guid);
            ChkEnergySaver.IsChecked = tag == "Min";
            RestoreBrightnessAfterSchemeChange();
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Activate();
        Topmost = true;

        // Сразу показать данные батареи
        var battery = BatteryHelper.GetStatus();
        UpdateBatteryTime(battery);

        // Синхронизировать галочку «Энергосбережение» с системой (powercfg ESBATTTHRESHOLD)
        try
        {
            _updatingSchemeUi = true;
            ChkEnergySaver.IsChecked = SystemEnergySaverHelper.IsEnabled();
        }
        finally { _updatingSchemeUi = false; }

        var b = BrightnessHelper.GetBrightness();
        if (b.HasValue)
        {
            _updatingBrightnessSlider = true;
            SliderBrightness.Value = b.Value;
            TbBrightness.Text = $"{b.Value}%";
            _updatingBrightnessSlider = false;
        }

        var v = VolumeHelper.GetVolume();
        if (v.HasValue)
        {
            _updatingVolumeSlider = true;
            SliderVolume.Value = v.Value;
            TbVolume.Text = $"{(int)(v.Value * 100)}%";
            _updatingVolumeSlider = false;
        }

        Activate();
    }

    private void SliderBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingBrightnessSlider) return;
        if (TbBrightness == null) return;
        var v = (byte)SliderBrightness.Value;
        TbBrightness.Text = $"{v}%";
        BrightnessHelper.SetBrightness(v);
    }

    private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingVolumeSlider) return;
        if (TbVolume == null) return;
        var v = (float)SliderVolume.Value;
        TbVolume.Text = $"{(int)(v * 100)}%";
        VolumeHelper.SetVolume(v);
    }

    private void BtnEyeProtection_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("ms-settings:nightlight") { UseShellExecute = true };
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("ms-settings:display") { UseShellExecute = true };
                System.Diagnostics.Process.Start(psi);
            }
            catch { }
        }
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        ExitConfirmOverlay.Visibility = Visibility.Visible;
    }

    private void BtnExitWithScheme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string tag) return;
        string? guid = tag switch { "Min" => _guidMin, "Balanced" => _guidBal, "Max" => _guidMax, _ => null };
        if (!string.IsNullOrEmpty(guid))
            PowerSchemeHelper.SetActiveScheme(guid);
        Close();
    }

    private void BtnExitCancel_Click(object sender, RoutedEventArgs e)
    {
        ExitConfirmOverlay.Visibility = Visibility.Collapsed;
    }

    private void ChkEnergySaver_Changed(object sender, RoutedEventArgs e)
    {
        if (ChkEnergySaver.IsChecked == true)
        {
            if (!string.IsNullOrEmpty(_guidMin))
            {
                _updatingSchemeUi = true;
                RbMin.IsChecked = true;
                _updatingSchemeUi = false;
                PowerSchemeHelper.SetActiveScheme(_guidMin);
            }
            SystemEnergySaverHelper.Enable();
        }
        else
        {
            if (!string.IsNullOrEmpty(_guidBal))
            {
                _updatingSchemeUi = true;
                RbBalanced.IsChecked = true;
                _updatingSchemeUi = false;
                PowerSchemeHelper.SetActiveScheme(_guidBal);
            }
            SystemEnergySaverHelper.Disable();
        }
    }

    private void ChkTopmost_Changed(object sender, RoutedEventArgs e)
    {
        Topmost = ChkTopmost.IsChecked == true;
    }

    private void CbUpdateInterval_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CbUpdateInterval?.SelectedIndex is not int idx || idx < 0) return;
        int[] intervals = { 500, 1000, 2000, 5000 };
        if (idx >= intervals.Length) return;
        _updateIntervalMs = intervals[idx];
        RestartSensorTimer(_updateIntervalMs);
    }

    private void CbAutoCheckInterval_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CbAutoCheckInterval?.SelectedIndex is not int idx || idx < 0) return;
        int[] intervals = { 1000, 2000, 3000, 5000, 10000 };
        if (idx >= intervals.Length) return;
        _autoCheckIntervalMs = intervals[idx];
    }

    private void UpdateBatteryTime((int? Percent, bool OnBattery, bool Charging, int? MinutesRemaining, int? MinutesToFull) b)
    {
        if (TbBatteryTime == null) return;
        if (!b.Percent.HasValue)
        {
            TbBatteryTime.Text = "— (от сети?)";
            return;
        }
        string pct = $"{b.Percent}%";
        if (b.Charging && b.MinutesToFull.HasValue && b.MinutesToFull.Value > 0)
        {
            int h = b.MinutesToFull.Value / 60;
            int m = b.MinutesToFull.Value % 60;
            TbBatteryTime.Text = h > 0 ? $"{pct}, ~{h} ч {m} мин до полной зарядки" : $"{pct}, ~{m} мин до полной зарядки";
        }
        else if (b.OnBattery && b.MinutesRemaining.HasValue && b.MinutesRemaining.Value > 0)
        {
            int h = b.MinutesRemaining.Value / 60;
            int m = b.MinutesRemaining.Value % 60;
            TbBatteryTime.Text = h > 0 ? $"{pct}, ~{h} ч {m} мин до разрядки" : $"{pct}, ~{m} мин до разрядки";
        }
        else if (b.OnBattery)
            TbBatteryTime.Text = $"{pct} (остаток времени неизвестен)";
        else
            TbBatteryTime.Text = $"{pct} (от сети)";
    }

    private static string GetRecommendedScheme(float? load, float? maxTemp,
        (int? Percent, bool OnBattery, bool Charging, int? MinutesRemaining, int? MinutesToFull) b)
    {
        float loadVal = load ?? 0;
        float tempVal = maxTemp ?? 0;
        int pct = b.Percent ?? 100;
        if (b.OnBattery && pct < 25) return "Min";
        if (b.OnBattery && pct < 40 && loadVal < 40) return "Min";
        if (tempVal >= 85 || loadVal >= 85) return "Max";
        if (b.OnBattery && loadVal >= 70) return "Max";
        if (!b.OnBattery && loadVal >= 65) return "Max";
        if (!b.OnBattery && loadVal < 25 && tempVal < 65) return "Min";
        return "Balanced";
    }

    private void UpdateRecommendedIndicator(float? load, float? maxTemp,
        (int? Percent, bool OnBattery, bool Charging, int? MinutesRemaining, int? MinutesToFull) battery)
    {
        _recommendedScheme = GetRecommendedScheme(load, maxTemp, battery);
        if (RecommendedIndicator == null) return;
        RecommendedIndicator.Fill = _recommendedScheme switch
        {
            "Min" => (Brush)FindResource("RecommendedMin"),
            "Max" => (Brush)FindResource("RecommendedMax"),
            _ => (Brush)FindResource("RecommendedBalanced")
        };
        if (_blinkTimer == null)
        {
            _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _blinkTimer.Tick += (_, _) =>
            {
                if (RecommendedIndicator != null)
                    RecommendedIndicator.Opacity = RecommendedIndicator.Opacity > 0.5 ? 0.35 : 0.95;
            };
            _blinkTimer.Start();
        }
    }

    private void ApplyRecommendedScheme(string scheme)
    {
        var active = PowerSchemeHelper.GetActiveSchemeGuid();
        string? guid = scheme switch { "Min" => _guidMin, "Max" => _guidMax, _ => _guidBal };
        if (string.IsNullOrEmpty(guid)) return;
        bool isMin = string.Equals(active, _guidMin, StringComparison.OrdinalIgnoreCase);
        bool isBal = string.Equals(active, _guidBal, StringComparison.OrdinalIgnoreCase);
        bool isMax = string.Equals(active, _guidMax, StringComparison.OrdinalIgnoreCase);
        bool needMin = scheme == "Min";
        bool needBal = scheme == "Balanced";
        bool needMax = scheme == "Max";
        if ((needMin && !isMin) || (needBal && !isBal) || (needMax && !isMax))
        {
            _updatingSchemeUi = true;
            PowerSchemeHelper.SetActiveScheme(guid);
            if (needMin) RbMin.IsChecked = true;
            else if (needMax) RbMax.IsChecked = true;
            else RbBalanced.IsChecked = true;
            _updatingSchemeUi = false;
            RestoreBrightnessAfterSchemeChange();
        }
    }

    private void RestoreBrightnessAfterSchemeChange()
    {
        byte level = (byte)Math.Clamp(SliderBrightness?.Value ?? 50, 0, 100);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try { BrightnessHelper.SetBrightness(level); } catch { }
        };
        timer.Start();
    }

    private void ChkAutoMode_Changed(object sender, RoutedEventArgs e)
    {
        _autoMode = ChkAutoMode?.IsChecked == true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _sensorTimer?.Dispose();
        _blinkTimer?.Stop();
        _sensorReader.Close();
        EyeProtectionHelper.Restore();
        base.OnClosed(e);
    }
}
