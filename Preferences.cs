using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
namespace LaptopBar;
public sealed class Preferences
{
 public static Preferences Current { get; } = Load();
 public bool Animations { get; set; } = true;
 public bool AutoHide { get; set; } = false;
 public int AutoHideSeconds { get; set; } = 8;
 public bool ShowQwen { get; set; } = true;
 public bool ShowOss { get; set; } = true;
 public bool ShowGpt { get; set; } = true;
 public string GptAppPath { get; set; } = "";
 public Dictionary<string,string> Endpoints { get; set; } = new() { ["Qwen"] = "http://127.0.0.1:1234/v1", ["Oss"] = "http://127.0.0.1:1234/v1" };
 private static string FilePath => Path.Combine(AppContext.BaseDirectory, "settings.json");
 private static Preferences Load() { try { return JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath)) ?? new(); } catch { return new(); } }
 public void Save() { try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this)); } catch { } }
 public static void OpenGpt() => GptLauncher.Open();
}
