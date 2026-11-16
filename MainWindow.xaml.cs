using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace LaptopBar;
public partial class MainWindow : Window
{
    private readonly SystemMetrics _metrics = new();
    private readonly GpuMetrics _gpu = new();
    private bool _gpuAvailable;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
    private Forms.NotifyIcon? _tray;
    private AiWindow? _ai;
    private StatusWindow? _connections;
    private SettingsWindow? _settings;
    private string _profile = "Qwen";
    private readonly DispatcherTimer _idleTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private DateTime _lastInteraction = DateTime.UtcNow;
    private bool _edgeHidden;
    private double _shownTop, _edgeTop;
    private bool _restoreTopmost;
    public MainWindow()
    {
        InitializeComponent();
        UpdateActiveButtons();
        MouseEnter += (_,_) => RevealBar();
        MouseLeave += (_,_) => _lastInteraction = DateTime.UtcNow;
        _idleTimer.Tick += (_,_) => CheckIdle();
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--test-ui") >= 0) ShowInTaskbar = true;
        Loaded += (_, _) =>
        {
            ApplyPreferences(); Tick(); _timer.Tick += (_, _) => Tick(); _timer.Start(); _idleTimer.Start();
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("상단바 표시 / 숨기기", null, (_, _) => Dispatcher.Invoke(ToggleBar));
            menu.Items.Add("로컬 AI", null, (_, _) => Dispatcher.Invoke(() => OpenAi(this, new RoutedEventArgs())));
            menu.Items.Add("종료", null, (_, _) => Dispatcher.Invoke(Close));
            _tray = new Forms.NotifyIcon { Icon = CreateTrayIcon(), Text = "LaptopBar · 더블클릭하여 표시 / 숨기기", ContextMenuStrip = menu, Visible = true };
            _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ToggleBar);
            SystemEvents.DisplaySettingsChanged += DisplayChanged;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--smoke-test") >= 0)
                Dispatcher.BeginInvoke(new Action(SmokeTest), DispatcherPriority.ApplicationIdle);
        };
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(new Action(PositionBar));
        Closed += (_, _) => { SystemEvents.DisplaySettingsChanged -= DisplayChanged; _timer.Stop(); _idleTimer.Stop(); _gpu.Dispose(); _tray?.Icon?.Dispose(); _tray?.Dispose(); _ai?.Close(); _connections?.Close(); _settings?.Close(); };
    }
    private void DisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(PositionBar));
    private void PositionBar()
    {
        var source = PresentationSource.FromVisual(this);
        var scale = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var area = Forms.Screen.PrimaryScreen!.WorkingArea;
        var origin = scale.Transform(new Point(area.Left, area.Top));
        double available = area.Width * scale.M11;
        MetricsPanel.Visibility = available < 620 ? Visibility.Collapsed : Visibility.Visible;
        Width = MeasureBarWidth(available);
        if (_edgeHidden) Topmost = _restoreTopmost;
        BeginAnimation(TopProperty,null); _edgeHidden = false;
        Left = origin.X + (available - Width) / 2; _edgeTop = origin.Y; Top = _shownTop = origin.Y + 8;
    }
    private double MeasureBarWidth(double available)
    {
        // Measure each group independently: Grid's previously arranged Auto columns can otherwise
        // retain their old minimum width during a visibility change.
        double contents = 0;
        foreach (var panel in new FrameworkElement[] { ModelPanel, MetricsPanel, RightPanel }) {
            if (panel.Visibility == Visibility.Collapsed) continue;
            panel.InvalidateMeasure(); panel.Measure(new Size(double.PositiveInfinity,Height - 10));
            contents += panel.DesiredSize.Width;
        }
        // Grid insets (24), frame (10), and a dedicated right gutter (24).
        return Math.Max(280,Math.Min(Math.Ceiling(contents) + 58,available - 24));
    }
    internal void ApplyPreferences()
    {
        QwenButton.Visibility = Preferences.Current.ShowQwen ? Visibility.Visible : Visibility.Collapsed;
        OssButton.Visibility = Preferences.Current.ShowOss ? Visibility.Visible : Visibility.Collapsed;
        GptButton.Visibility = Preferences.Current.ShowGpt ? Visibility.Visible : Visibility.Collapsed;
        if ((_profile == "Qwen" && !Preferences.Current.ShowQwen) || (_profile == "Oss" && !Preferences.Current.ShowOss)) _ai?.Close();
        PositionBar(); _lastInteraction = DateTime.UtcNow;
    }
    private void CheckIdle(bool test = false)
    {
        if (!Preferences.Current.AutoHide || !IsVisible) { if (_edgeHidden) RevealBar(); return; }
        if ((!test && IsMouseOver) || _ai != null || _connections != null || _settings != null) { _lastInteraction = DateTime.UtcNow; return; }
        if (!_edgeHidden && (DateTime.UtcNow - _lastInteraction).TotalSeconds >= Math.Clamp(Preferences.Current.AutoHideSeconds,3,60)) HideAtEdge();
    }
    private void HideAtEdge()
    {
        if (_edgeHidden) return;
        _shownTop = Top; _restoreTopmost = Topmost; Topmost = true; _edgeHidden = true;
        SlideTo(_edgeTop - Height + 9);
    }
    private void RevealBar()
    {
        _lastInteraction = DateTime.UtcNow;
        if (!_edgeHidden) return;
        _edgeHidden = false; SlideTo(_shownTop); Topmost = _restoreTopmost;
    }
    private void SlideTo(double top)
    {
        double from = Top; BeginAnimation(TopProperty,null); Top = top;
        if (Preferences.Current.Animations) BeginAnimation(TopProperty,new DoubleAnimation(from,top,TimeSpan.FromMilliseconds(190)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }
    private void Tick()
    {
        _metrics.Update();
        CpuText.Text = $"{_metrics.Cpu:0}%"; CpuBar.Value = _metrics.Cpu;
        RamText.Text = $"{_metrics.UsedGb:0.0}G"; RamBar.Value = _metrics.RamPercent;
        BatteryText.Text = $"BAT {_metrics.Battery}";
        var wifi = Connections.Wifi(); WifiGlyph.Opacity = wifi.connected ? 1 : 0.4;
        SetGpuDisplay(_gpu.Read());
        var now = DateTime.Now;
        ClockText.Text = now.ToString("HH:mm"); DateText.Text = now.ToString("ddd, MMM dd", CultureInfo.InvariantCulture).ToUpperInvariant();
    }
    private void SetGpuDisplay((double percent,string engine)? reading)
    {
        bool available = reading.HasValue;
        GpuPanel.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
        if (reading is { } gpu) { GpuText.Text = $"{gpu.percent:0}%"; GpuBar.Value = gpu.percent; }
        _gpuAvailable = available;
        double center = Left + Width / 2;
        var source = PresentationSource.FromVisual(this);
        var scale = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var area = Forms.Screen.PrimaryScreen!.WorkingArea;
        Width = MeasureBarWidth(area.Width * scale.M11);
        Left = Math.Clamp(center - Width / 2,area.Left * scale.M11,Math.Max(area.Left * scale.M11,(area.Right * scale.M11) - Width));
    }
    private void DragBar(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase) return;
        if (e.ChangedButton == MouseButton.Left) { RevealBar(); double current = Top; BeginAnimation(TopProperty,null); Top = current; DragMove(); _shownTop = Top; _lastInteraction = DateTime.UtcNow; }
    }
    private void ToggleBar() { if (IsVisible) Hide(); else { PositionBar(); Show(); } }
    private void OpenConnections(object sender, RoutedEventArgs e)
    {
        RevealBar();
        if (_connections is not null) { _connections.Close(); return; }
        _connections = new StatusWindow();
        _connections.Closed += (_, _) => { _connections = null; UpdateActiveButtons(); };
        _connections.Left = Math.Clamp(Left + Width - 370, SystemParameters.WorkArea.Left, Math.Max(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Right - 350));
        _connections.Top = Top + Height + 6;
        _connections.Show();
        UpdateActiveButtons();
    }
    private void OpenAi(object sender, RoutedEventArgs e)
    {
        RevealBar();
        _settings?.Close();
        if (_ai is not null) { _ai.Activate(); return; }
        _ai = new AiWindow(_profile);
        var indicator = _profile == "Qwen" ? QwenDot : OssDot;
        _ai.ConnectionChanged += online => indicator.Fill = new SolidColorBrush(online ? Color.FromRgb(176,143,255) : Color.FromRgb(119,115,132));
        _ai.Closed += (_, _) => { _ai = null; UpdateActiveButtons(); };
        _ai.Show();
        _ai.Left = Math.Clamp(Left + Width - _ai.Width, SystemParameters.WorkArea.Left, Math.Max(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Right - _ai.Width));
        _ai.Top = Top + Height + 6;
        UpdateActiveButtons();
    }
    private void ShowMenu(object sender, RoutedEventArgs e)
    {
        RevealBar();
        if (_settings != null) { _settings.Close(); return; }
        _settings = new SettingsWindow(this,PositionBar);
        _settings.Closed += (_,_) => { _settings = null; UpdateActiveButtons(); };
        _settings.Left = Math.Clamp(Left + Width - 320,SystemParameters.WorkArea.Left,Math.Max(SystemParameters.WorkArea.Left,SystemParameters.WorkArea.Right - 310));
        _settings.Top = Top + Height + 6; _settings.Show(); UpdateActiveButtons();
    }
    private void SelectProfile(object sender, RoutedEventArgs e)
    {
        string profile = (string)((Button)sender).Tag;
        _settings?.Close();
        if (profile == "GPT") { _ai?.Close(); UpdateActiveButtons(); try { Preferences.OpenGpt(); } catch (Exception ex) { MessageBox.Show(ex.Message,"GPT 열기"); } return; }
        if (_profile != profile) { _ai?.Close(); _profile = profile; }
        OpenAi(this,e);
    }
    private void UpdateActiveButtons()
    {
        var active = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#494295"));
        QwenButton.Background = _ai != null && _profile == "Qwen" ? active : Brushes.Transparent;
        OssButton.Background = _ai != null && _profile == "Oss" ? active : Brushes.Transparent;
        SettingsButton.Background = _settings != null ? active : Brushes.Transparent;
        ConnectionsButton.Background = _connections != null ? active : Brushes.Transparent;
    }
    private static System.Drawing.Icon CreateTrayIcon()
    {
        using var bitmap = new System.Drawing.Bitmap(32,32); using var canvas = System.Drawing.Graphics.FromImage(bitmap);
        canvas.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var bg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(85,76,199)); canvas.FillEllipse(bg,1,1,30,30);
        using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(239,231,255),3); canvas.DrawLines(pen,new[] { new System.Drawing.Point(12,8),new System.Drawing.Point(12,23),new System.Drawing.Point(22,23) });
        var handle = bitmap.GetHicon(); try { using var icon = System.Drawing.Icon.FromHandle(handle); return (System.Drawing.Icon)icon.Clone(); } finally { DestroyIcon(handle); }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    private async void SmokeTest()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "verification"); Directory.CreateDirectory(folder);
        UpdateLayout();
        var image = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(this);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using (var file = File.Create(Path.Combine(folder, "topbar.png"))) encoder.Save(file);
        var chat = new AiWindow(); chat.Show(); await System.Threading.Tasks.Task.Delay(450); chat.UpdateLayout();
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--api-test") >= 0)
        {
            try {
                bool qwenOnline = false; chat.ConnectionChanged += online => qwenOnline = online;
                await chat.VerifyLocalApiAsync();
                if (!qwenOnline) throw new InvalidOperationException("Qwen online indicator failed");
                var oss = new AiWindow("Oss"); oss.Show(); await System.Threading.Tasks.Task.Delay(450);
                bool ossOnline = false; oss.ConnectionChanged += online => ossOnline = online;
                await oss.VerifyLocalApiAsync();
                if (!ossOnline) throw new InvalidOperationException("Oss online indicator failed");
                oss.Close(); if (ossOnline) throw new InvalidOperationException("Offline indicator failed");
                File.WriteAllText(Path.Combine(folder, "api-test.txt"), "PASS: Qwen and Oss profile selection; GET models; POST chat; SSE content/history; online/offline events.");
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(folder, "api-test.txt"), "FAIL: " + ex); }
        }
        var chatImage = new RenderTargetBitmap((int)chat.ActualWidth, (int)chat.ActualHeight, 96, 96, PixelFormats.Pbgra32); chatImage.Render(chat);
        var chatEncoder = new PngBitmapEncoder(); chatEncoder.Frames.Add(BitmapFrame.Create(chatImage));
        using (var file = File.Create(Path.Combine(folder, "chat.png"))) chatEncoder.Save(file);
        chat.Close();
        var ossPreview = new AiWindow("Oss"); ossPreview.Show(); await System.Threading.Tasks.Task.Delay(450); ossPreview.UpdateLayout();
        var ossImage = new RenderTargetBitmap((int)ossPreview.ActualWidth,(int)ossPreview.ActualHeight,96,96,PixelFormats.Pbgra32); ossImage.Render(ossPreview);
        var ossEncoder = new PngBitmapEncoder(); ossEncoder.Frames.Add(BitmapFrame.Create(ossImage));
        using (var file = File.Create(Path.Combine(folder,"oss.png"))) ossEncoder.Save(file); ossPreview.Close();
        var connections = new StatusWindow(); connections.Show(); await System.Threading.Tasks.Task.Delay(450); connections.UpdateLayout();
        var statusImage = new RenderTargetBitmap((int)connections.ActualWidth, (int)connections.ActualHeight, 96, 96, PixelFormats.Pbgra32); statusImage.Render(connections);
        var statusEncoder = new PngBitmapEncoder(); statusEncoder.Frames.Add(BitmapFrame.Create(statusImage));
        using (var file = File.Create(Path.Combine(folder, "connections.png"))) statusEncoder.Save(file);
        connections.Close();
        var settings = new SettingsWindow(this, PositionBar); settings.Show(); await System.Threading.Tasks.Task.Delay(450); settings.UpdateLayout();
        var settingsImage = new RenderTargetBitmap((int)settings.ActualWidth,(int)settings.ActualHeight,96,96,PixelFormats.Pbgra32); settingsImage.Render(settings);
        var settingsEncoder = new PngBitmapEncoder(); settingsEncoder.Frames.Add(BitmapFrame.Create(settingsImage));
        using (var file = File.Create(Path.Combine(folder,"settings.png"))) settingsEncoder.Save(file);
        settings.Close();
        _idleTimer.Stop();
        var prefs = Preferences.Current; bool savedAuto = prefs.AutoHide, savedQwen = prefs.ShowQwen, savedOss = prefs.ShowOss, savedGpt = prefs.ShowGpt;
        try {
            prefs.ShowQwen = prefs.ShowOss = prefs.ShowGpt = false; ApplyPreferences();
            if (QwenButton.IsVisible || OssButton.IsVisible || GptButton.IsVisible) throw new InvalidOperationException("AI visibility failed");
            prefs.ShowQwen = savedQwen; prefs.ShowOss = savedOss; prefs.ShowGpt = savedGpt; prefs.AutoHide = true; ApplyPreferences();
            _lastInteraction = DateTime.UtcNow.AddSeconds(-65); CheckIdle(true); await System.Threading.Tasks.Task.Delay(250);
            if (!_edgeHidden || Math.Abs(Top - (_edgeTop - Height + 9)) > 1) throw new InvalidOperationException("Auto-hide transition failed");
            RaiseEvent(new System.Windows.Input.MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount) { RoutedEvent = MouseEnterEvent }); await System.Threading.Tasks.Task.Delay(250);
            if (_edgeHidden || Math.Abs(Top - _shownTop) > 1) throw new InvalidOperationException("Hover reveal failed");
            HideAtEdge(); prefs.AutoHide = false; CheckIdle(); await System.Threading.Tasks.Task.Delay(250);
            if (_edgeHidden) throw new InvalidOperationException("Auto-hide OFF failed");
            File.WriteAllText(Path.Combine(folder,"ui-test.txt"),"PASS: AI checkbox visibility; idle edge hide; MouseEnter reveal; auto-hide OFF restore.");
        } catch(Exception ex) { File.WriteAllText(Path.Combine(folder,"ui-test.txt"),"FAIL: " + ex); }
        finally { prefs.AutoHide = savedAuto; prefs.ShowQwen = savedQwen; prefs.ShowOss = savedOss; prefs.ShowGpt = savedGpt; ApplyPreferences(); }
        try {
            static bool Active(Button button) => button.Background is SolidColorBrush brush && brush.Color.A > 0;
            ShowMenu(this,new RoutedEventArgs());
            if (_settings == null || !Active(SettingsButton)) throw new InvalidOperationException("Settings active state failed");
            SelectProfile(QwenButton,new RoutedEventArgs());
            if (_settings != null || Active(SettingsButton) || _ai == null || !Active(QwenButton) || Active(OssButton)) throw new InvalidOperationException("AI click/settings close failed");
            _ai.Close();
            if (Active(QwenButton) || Active(OssButton)) throw new InvalidOperationException("AI close highlight reset failed");
            SelectProfile(OssButton,new RoutedEventArgs());
            if (!Active(OssButton) || Active(QwenButton)) throw new InvalidOperationException("Oss active state failed");
            _ai!.Close();
            if (Active(OssButton)) throw new InvalidOperationException("Oss highlight reset failed");
            ShowMenu(this,new RoutedEventArgs()); _settings!.Close();
            if (Active(SettingsButton)) throw new InvalidOperationException("Settings highlight reset failed");
            SetGpuDisplay((23,"test_engine"));
            if (GpuPanel.Visibility != Visibility.Visible || GpuBar.Value != 23) throw new InvalidOperationException("GPU visible state failed");
            double gpuWidth = Width;
            SetGpuDisplay(null);
            if (GpuPanel.Visibility != Visibility.Collapsed || Width >= gpuWidth) throw new InvalidOperationException("Missing GPU hide failed");
            var realGpu = _gpu.Read(); SetGpuDisplay(realGpu);
            File.WriteAllText(Path.Combine(folder,"popup-gpu-test.txt"),"PASS: settings closes on AI; Qwen/Oss/settings close clears highlight; GPU present/absent layout. Actual GPU: " + (realGpu.HasValue ? realGpu.Value.percent.ToString("F1") + "% · " + realGpu.Value.engine : "unavailable (hidden)"));
        } catch(Exception ex) { File.WriteAllText(Path.Combine(folder,"popup-gpu-test.txt"),"FAIL: " + ex); }
        try {
            for (int mask = 0; mask < 8; mask++) {
                prefs.ShowQwen = (mask & 1) != 0; prefs.ShowOss = (mask & 2) != 0; prefs.ShowGpt = (mask & 4) != 0; ApplyPreferences();
                for (int gpu = 0; gpu < 2; gpu++) {
                    SetGpuDisplay(gpu == 1 ? (100,"layout_test") : null); UpdateLayout(); await System.Threading.Tasks.Task.Delay(40); UpdateLayout();
                    double right = SettingsButton.TranslatePoint(new Point(SettingsButton.ActualWidth,0),this).X;
                    if (right > ActualWidth - 18) throw new InvalidOperationException($"Settings overflow: mask={mask}, GPU={gpu}, right={right}, window={ActualWidth}");
                }
            }
            if (SettingsButton.ToolTip != null || ConnectionsButton.ToolTip != null || RamText.ToolTip != null || BatteryText.ToolTip != null || GpuPanel.ToolTip != null) throw new InvalidOperationException("Unexpected main-bar tooltip");
            File.WriteAllText(Path.Combine(folder,"layout-test.txt"),"PASS: settings button inside right edge with >=18 DIP clearance; 8 AI combinations x GPU present/absent; main-bar tooltips removed.");
        } catch(Exception ex) { File.WriteAllText(Path.Combine(folder,"layout-test.txt"),"FAIL: " + ex); }
        finally { prefs.ShowQwen = savedQwen; prefs.ShowOss = savedOss; prefs.ShowGpt = savedGpt; SetGpuDisplay(_gpu.Read()); ApplyPreferences(); }
        File.WriteAllText(Path.Combine(folder, "metrics.txt"), $"RAM={_metrics.UsedGb:F2}/{_metrics.TotalGb:F2} GB; battery={_metrics.Battery}; power={_metrics.PowerDetail}; DPI={VisualTreeHelper.GetDpi(this).PixelsPerDip}; width={Width}; appMB={Process.GetCurrentProcess().WorkingSet64 / 1048576.0:F1}");
        Close();
    }
}
