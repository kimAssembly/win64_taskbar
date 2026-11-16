using System;
using Microsoft.Win32;
namespace LaptopBar;
public static class StartupRegistration
{
 private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
 private const string EntryName = "LaptopBar";
 public static bool Enabled { get { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return string.Equals(key?.GetValue(EntryName) as string,Command,StringComparison.OrdinalIgnoreCase); } }
 public static string Command => "\"" + Environment.ProcessPath + "\"";
 public static void SetEnabled(bool enabled)
 {
  using var key = Registry.CurrentUser.CreateSubKey(RunKey);
  if (enabled) key.SetValue(EntryName,Command,RegistryValueKind.String);
  else if (string.Equals(key.GetValue(EntryName) as string,Command,StringComparison.OrdinalIgnoreCase)) key.DeleteValue(EntryName,false);
 }
}
