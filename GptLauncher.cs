using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
namespace LaptopBar;
public static class GptLauncher
{
 public static string ResolveInstalledApp()
 {
  // The current unified ChatGPT package retains its OpenAI.Codex Windows identity.
  // Query AppsFolder so Classic and future package identities also work.
  object? shell = null, folder = null, items = null;
  try {
   shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
   folder = ((dynamic)shell!).NameSpace("shell:AppsFolder");
   items = ((dynamic)folder!).Items();
   string classic = "";
   foreach (object entry in (dynamic)items) {
    try { dynamic item = entry; string name = (string)item.Name; string id = (string)item.Path;
     if (string.Equals(name,"ChatGPT",StringComparison.OrdinalIgnoreCase)) return id;
     if (string.Equals(name,"ChatGPT Classic",StringComparison.OrdinalIgnoreCase)) classic = id;
    } finally { if (Marshal.IsComObject(entry)) Marshal.ReleaseComObject(entry); }
   }
   return classic;
  } catch { return ""; }
  finally { if(items!=null && Marshal.IsComObject(items)) Marshal.ReleaseComObject(items); if(folder!=null && Marshal.IsComObject(folder)) Marshal.ReleaseComObject(folder); if(shell!=null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell); }
 }
 public static string Open()
 {
  string path = Preferences.Current.GptAppPath;
  if(File.Exists(path) && path.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)) { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); return path; }
  string appId = ResolveInstalledApp();
  if (!string.IsNullOrEmpty(appId)) {
   object manager = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"))!)!;
   try { Marshal.ThrowExceptionForHR(((IApplicationActivationManager)manager).ActivateApplication(appId,null,0,out _)); return appId; }
   finally { Marshal.ReleaseComObject(manager); }
  }
  Process.Start(new ProcessStartInfo("https://chatgpt.com/") { UseShellExecute = true }); return "https://chatgpt.com/";
 }
 [ComImport,Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 private interface IApplicationActivationManager { [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appId,[MarshalAs(UnmanagedType.LPWStr)] string? arguments,uint options,out uint processId); }
}
