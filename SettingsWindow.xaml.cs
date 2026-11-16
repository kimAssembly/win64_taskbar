using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
namespace LaptopBar;
public partial class SettingsWindow : Window
{
 private readonly MainWindow _bar; private readonly Action _recenter;
 private bool _loading = true;
 public SettingsWindow(MainWindow bar, Action recenter) { InitializeComponent(); _bar = bar; _recenter = recenter; QwenCheck.IsChecked = Preferences.Current.ShowQwen; OssCheck.IsChecked = Preferences.Current.ShowOss; GptCheck.IsChecked = Preferences.Current.ShowGpt; _loading = false; Refresh(); RefreshAutoHide(); PopupMotion.Attach(this); }
 private void ModelsChanged(object sender,RoutedEventArgs e) { if(_loading) return; var p = Preferences.Current; p.ShowQwen = QwenCheck.IsChecked == true; p.ShowOss = OssCheck.IsChecked == true; p.ShowGpt = GptCheck.IsChecked == true; p.Save(); _bar.ApplyPreferences(); }
 private void ToggleAutoHide(object sender,RoutedEventArgs e) { Preferences.Current.AutoHide = !Preferences.Current.AutoHide; SaveAutoHide(); }
 private void LessDelay(object sender,RoutedEventArgs e) { Preferences.Current.AutoHideSeconds = Math.Max(3,Preferences.Current.AutoHideSeconds - 1); SaveAutoHide(); }
 private void MoreDelay(object sender,RoutedEventArgs e) { Preferences.Current.AutoHideSeconds = Math.Min(60,Preferences.Current.AutoHideSeconds + 1); SaveAutoHide(); }
 private void SaveAutoHide() { Preferences.Current.Save(); _bar.ApplyPreferences(); RefreshAutoHide(); }
 private void RefreshAutoHide() { AutoHideButton.Content = Preferences.Current.AutoHide ? "●    미사용 시 자동 숨기기   ON" : "○    미사용 시 자동 숨기기   OFF"; DelayText.Text = Preferences.Current.AutoHideSeconds + "초"; }
 private void Refresh() { StartupButton.Content = StartupRegistration.Enabled ? "●    Windows 시작 시 실행   ON" : "○    Windows 시작 시 실행   OFF"; PinButton.Content = _bar.Topmost ? "●    항상 위에 표시   ON" : "○    항상 위에 표시   OFF"; MotionButton.Content = Preferences.Current.Animations ? "✦    펼치기 애니메이션   ON" : "✧    펼치기 애니메이션   OFF"; GptHint.Text = string.IsNullOrWhiteSpace(Preferences.Current.GptAppPath) ? "ChatGPT 앱 자동 감지 · 미설치 시 웹" : Path.GetFileName(Preferences.Current.GptAppPath); }
 private void ToggleStartup(object sender,RoutedEventArgs e) { try { StartupRegistration.SetEnabled(!StartupRegistration.Enabled); Refresh(); } catch(Exception ex) { GptHint.Text = "시작 등록 실패: " + ex.Message; } }
 private void TogglePin(object sender,RoutedEventArgs e) { _bar.Topmost = !_bar.Topmost; Refresh(); }
 private void ToggleMotion(object sender,RoutedEventArgs e) { Preferences.Current.Animations = !Preferences.Current.Animations; Preferences.Current.Save(); Refresh(); }
 private void Recenter(object sender,RoutedEventArgs e) { _recenter(); Close(); }
 private void ChooseApp(object sender,RoutedEventArgs e) { var picker = new OpenFileDialog { Filter = "앱 실행 파일 (*.exe)|*.exe", Title = "ChatGPT 앱 실행 파일 선택" }; if(picker.ShowDialog(this)==true) { Preferences.Current.GptAppPath=picker.FileName; Preferences.Current.Save(); Refresh(); } }
 private void HideBar(object sender,RoutedEventArgs e) { _bar.Hide(); Close(); }
 private void ExitBar(object sender,RoutedEventArgs e) => _bar.Close();
 private void ClosePanel(object sender,RoutedEventArgs e) => Close();
}
