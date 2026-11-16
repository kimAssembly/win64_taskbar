using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Linq;

namespace LaptopBar;
public partial class AiWindow : Window
{
    public event Action<bool>? ConnectionChanged;
    private readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false }) { Timeout = Timeout.InfiniteTimeSpan };
    private readonly List<object> _history = new();
    private CancellationTokenSource? _request;
    private Uri? _base;
    private bool _busy;
    private readonly string _profile;
    private bool _closed;
    private readonly DispatcherTimer _health = new() { Interval = TimeSpan.FromSeconds(12) };
    public AiWindow(string profile = "Qwen")
    {
        _profile = profile;
        InitializeComponent();
        ProfileTitle.Text = profile.ToUpperInvariant() + " / PROMPT";
        ModelLogo.Source = ModelLogos.Create(profile);
        if (Preferences.Current.Endpoints.TryGetValue(profile, out var endpoint)) Endpoint.Text = endpoint;
        PopupMotion.Attach(this);
        Endpoint.TextChanged += (_, _) => { _base = null; SendButton.IsEnabled = false; ConnectionChanged?.Invoke(false); Status.Text = "주소 변경됨 · 연결을 다시 눌러주세요."; };
        _health.Tick += async (_, _) => {
            if (_busy || _base == null || _closed) return;
            try {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                using var response = await _http.GetAsync(new Uri(_base,"models"),timeout.Token);
                response.EnsureSuccessStatusCode();
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                bool present = doc.RootElement.GetProperty("data").EnumerateArray().Any(m => m.GetProperty("id").GetString() == Models.SelectedItem as string);
                if (!_closed) { ConnectionChanged?.Invoke(present); SendButton.IsEnabled = present; if (!present) Status.Text = "선택한 모델이 서버 목록에 없습니다. 다시 연결하세요."; }
            } catch { if (!_closed) { ConnectionChanged?.Invoke(false); SendButton.IsEnabled = false; Status.Text = "서버 연결 끊김 · 다시 연결하세요."; } }
        };
        _health.Start();
        Closed += (_, _) => { _closed = true; _health.Stop(); _request?.Cancel(); _http.Dispose(); ConnectionChanged?.Invoke(false); };
    }
    private static Uri LocalBase(string text)
    {
        if (!Uri.TryCreate(text.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri) || uri.Scheme != "http" || !uri.IsLoopback || !string.IsNullOrEmpty(uri.UserInfo) || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidOperationException("http://127.0.0.1:포트/v1 형태의 로컬 주소를 입력하세요.");
        return uri;
    }
    private async void Connect(object sender, RoutedEventArgs e)
    {
        _base = null; Models.Items.Clear(); SendButton.IsEnabled = false; ConnectButton.IsEnabled = false; ConnectionChanged?.Invoke(false);
        try
        {
            var candidate = LocalBase(Endpoint.Text);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            using var response = await _http.GetAsync(new Uri(candidate, "models"), timeout.Token); response.EnsureSuccessStatusCode();
            using var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            if (_closed) return;
            foreach (var model in data.RootElement.GetProperty("data").EnumerateArray()) if (model.TryGetProperty("id", out var id)) Models.Items.Add(id.GetString());
            if (Models.Items.Count == 0) throw new InvalidOperationException("모델이 없어요. 실행기에서 모델을 로드하세요.");
            var preferred = Models.Items.Cast<string>().FirstOrDefault(id => id.Contains(_profile == "Oss" ? "oss" : "qwen", StringComparison.OrdinalIgnoreCase));
            Models.SelectedItem = preferred;
            if (preferred == null) { Status.Text = _profile + " 모델이 없습니다. 실행기에서 해당 모델을 로드하세요."; return; }
            _history.Clear(); Conversation.Children.Clear();
            _base = candidate; SendButton.IsEnabled = true;
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"--api-test") < 0) { Preferences.Current.Endpoints[_profile] = candidate.ToString().TrimEnd('/'); Preferences.Current.Save(); }
            Status.Text = "연결됨 · 로컬 서버가 보고한 모델 목록입니다."; ConnectionChanged?.Invoke(true);
        }
        catch (Exception ex) { Status.Text = "연결 실패 · " + (ex is OperationCanceledException ? "서버 응답 시간 초과" : ex.Message); }
        finally { ConnectButton.IsEnabled = true; }
    }
    private TextBlock Bubble(string role, string text)
    {
        Conversation.Children.Add(new TextBlock { Text = role, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(184,172,255)), Margin = new Thickness(0,12,0,5) });
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, LineHeight = 20, FontFamily = new FontFamily("Consolas, Malgun Gothic") };
        Conversation.Children.Add(block); return block;
    }
    private async void Send(object sender, RoutedEventArgs e)
    {
        if (_busy || _base is null || Models.SelectedItem is not string model || string.IsNullOrWhiteSpace(Prompt.Text)) return;
        string question = Prompt.Text.Trim(); Prompt.Clear();
        if (_history.Count == 0) Conversation.Children.Clear();
        Bubble("YOU", question); var answer = Bubble("LOCAL / " + model, "생성 중…");
        var messages = new List<object>(_history) { new { role = "user", content = question } };
        _request = new CancellationTokenSource(TimeSpan.FromMinutes(10)); SetBusy(true);
        var generated = new StringBuilder();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_base, "chat/completions")) { Content = new StringContent(JsonSerializer.Serialize(new { model, messages, stream = true, max_tokens = 2048 }), Encoding.UTF8, "application/json") };
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _request.Token); response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync(_request.Token); using var reader = new StreamReader(stream);
            while (await reader.ReadLineAsync(_request.Token) is string line)
            {
                if (!line.StartsWith("data:")) continue;
                string payload = line[5..].Trim(); if (payload == "[DONE]") break; if (payload.Length == 0) continue;
                using var chunk = JsonDocument.Parse(payload);
                if (chunk.RootElement.TryGetProperty("error", out var error)) throw new InvalidOperationException(error.ToString());
                if (!chunk.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0) continue;
                if (choices[0].TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                { generated.Append(content.GetString()); answer.Text = generated.ToString(); ConversationScroll.ScrollToEnd(); }
            }
            if (generated.Length == 0) { answer.Text = "답변 본문이 없습니다. 모델 또는 채팅 템플릿 설정을 확인하세요."; return; }
            _history.Add(new { role = "user", content = question }); _history.Add(new { role = "assistant", content = generated.ToString() });
            // Bound context: six recent turns; max output remains explicitly capped above.
            while (_history.Count > 12) { _history.RemoveRange(0, 2); }
            Status.Text = "완료 · 최근 6회 대화 유지 · 파일/명령 실행 기능 없음";
        }
        catch (OperationCanceledException) { answer.Text = generated.Length == 0 ? "생성이 중지되었습니다." : generated + "\n[중지됨]"; Status.Text = "생성 중지 · 이번 응답은 다음 대화에 포함되지 않습니다."; }
        catch (Exception ex) { answer.Text = "요청 실패 · " + ex.Message; ConnectionChanged?.Invoke(false); Status.Text = "서버 상태와 컨텍스트 길이를 확인하세요."; }
        finally { SetBusy(false); _request.Dispose(); _request = null; }
    }
    private void SetBusy(bool busy) { _busy = busy; SendButton.IsEnabled = !busy && _base is not null; StopButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed; ConnectButton.IsEnabled = !busy; Endpoint.IsEnabled = !busy; Models.IsEnabled = !busy; }
    private void Stop(object sender, RoutedEventArgs e) => _request?.Cancel();
    private void Clear(object sender, RoutedEventArgs e) { if (_busy) return; _history.Clear(); Conversation.Children.Clear(); Status.Text = "대화를 비웠습니다."; }
    private void PromptKey(object sender, KeyEventArgs e) { if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { e.Handled = true; Send(this, new RoutedEventArgs()); } }
    private void ClosePanel(object sender, RoutedEventArgs e) => Close();
    private void DragHeader(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); }
    internal async Task VerifyLocalApiAsync()
    {
        Endpoint.Text = "http://127.0.0.1:18347/v1";
        Connect(this, new RoutedEventArgs());
        while (!ConnectButton.IsEnabled) await Task.Delay(30);
        if (_base == null) throw new InvalidOperationException(Status.Text);
        Prompt.Text = "test-message"; Send(this, new RoutedEventArgs());
        while (_busy) await Task.Delay(30);
        if (_history.Count != 2 || !JsonSerializer.Serialize(_history).Contains("Stream test passed.")) throw new InvalidOperationException("API stream verification failed");
    }
}
