using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace LaptopBar;
public partial class StatusWindow : Window
{
    private bool _refreshing;
    private bool _closing;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(4) };
    public StatusWindow()
    {
        InitializeComponent();
        PopupMotion.Attach(this);
        Loaded += (_, _) => { Refresh(); _timer.Tick += (_, _) => Refresh(); _timer.Start(); };
        Closed += (_, _) => _timer.Stop();
        Closing += (_, _) => _closing = true;
        Deactivated += (_, _) => { if (!_closing) Dispatcher.BeginInvoke(new Action(() => { if (!_closing) Close(); })); };
    }
    private void Refresh()
    {
        _refreshing = true;
        try
        {
            var wifi = Connections.Wifi(); WifiState.Text = wifi.connected ? "연결됨 · " + wifi.detail : wifi.detail; WifiTile.Background = Brush(wifi.connected ? "#554CC7" : "#302B51");
            var bt = Connections.Bluetooth(); BtState.Text = bt.detail; BtTile.Background = Brush(bt.enabled ? "#494295" : "#302B51");
            var speaker = Audio.Read(false); SpeakerSlider.IsEnabled = speaker.available; SpeakerSlider.Value = speaker.volume; SpeakerValue.Text = speaker.available ? $"{speaker.volume:0}%" : "—"; SpeakerLabel.Text = !speaker.available ? "출력 장치 없음" : speaker.muted ? "기본 스피커 · 음소거" : "기본 스피커";
            var mic = Audio.Read(true); MicSlider.IsEnabled = mic.available; MicSlider.Value = mic.volume; MicValue.Text = mic.available ? $"{mic.volume:0}%" : "—"; MicLabel.Text = !mic.available ? "입력 장치 없음" : mic.muted ? "기본 마이크 · 음소거" : "기본 마이크";
        }
        finally { _refreshing = false; }
    }
    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
    private void SpeakerChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (!_refreshing) ChangeVolume(false, e.NewValue); }
    private void MicChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (!_refreshing) ChangeVolume(true, e.NewValue); }
    private void ChangeVolume(bool mic, double value) { try { Audio.Set(mic, value); (mic ? MicValue : SpeakerValue).Text = $"{value:0}%"; } catch { Hint.Text = "장치가 변경되었습니다. 잠시 후 다시 시도하세요."; } }
    private void MuteSpeaker(object sender, RoutedEventArgs e) => Mute(false);
    private void MuteMic(object sender, RoutedEventArgs e) => Mute(true);
    private void Mute(bool mic) { try { Audio.ToggleMute(mic); Refresh(); } catch { Hint.Text = "오디오 장치를 사용할 수 없습니다."; } }
    private void WifiSettings(object sender, RoutedEventArgs e) => Settings("ms-settings:network-wifi");
    private void BtSettings(object sender, RoutedEventArgs e) => Settings("ms-settings:bluetooth");
    private void Settings(string uri) { try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); } catch { Hint.Text = "Windows 설정을 열지 못했습니다."; } }
    private void ClosePanel(object sender, RoutedEventArgs e) => Close();
}
