using System;
using System.Threading;
using System.Windows;

namespace LaptopBar;
public partial class App : Application
{
    private Mutex? _mutex;
    protected override void OnStartup(StartupEventArgs e)
    {
        if (Array.IndexOf(e.Args,"--enable-startup") >= 0 || Array.IndexOf(e.Args,"--gpt-launch-test") >= 0)
        {
            string folder = System.IO.Path.Combine(AppContext.BaseDirectory,"verification"); System.IO.Directory.CreateDirectory(folder);
            if (Array.IndexOf(e.Args,"--enable-startup") >= 0) { StartupRegistration.SetEnabled(true); System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"startup.txt"),StartupRegistration.Enabled ? "PASS: " + StartupRegistration.Command : "FAIL"); }
            if (Array.IndexOf(e.Args,"--gpt-launch-test") >= 0) System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"gpt-launch.txt"),GptLauncher.Open());
            Shutdown(); return;
        }
        _mutex = new Mutex(true, "Local\\LaptopBar.Prototype", out bool first);
        if (!first) { Shutdown(); return; }
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e) { _mutex?.Dispose(); base.OnExit(e); }
}
