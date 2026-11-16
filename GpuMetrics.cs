using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace LaptopBar;
public sealed class GpuMetrics : IDisposable
{
    private IntPtr _query, _counter;
    public GpuMetrics()
    {
        try {
            if (PdhOpenQueryW(null,UIntPtr.Zero,out _query) != 0) { Dispose(); return; }
            if (PdhAddEnglishCounterW(_query,@"\GPU Engine(*)\Utilization Percentage",UIntPtr.Zero,out _counter) != 0) { Dispose(); return; }
            PdhCollectQueryData(_query); // Rate counters require two samples.
        } catch { Dispose(); }
    }
    public (double percent,string engine)? Read()
    {
        if (_query == IntPtr.Zero) return null;
        try {
            if (PdhCollectQueryData(_query) != 0) return null;
            for (int attempt = 0; attempt < 2; attempt++) {
                uint bytes = 0;
                uint result = PdhGetFormattedCounterArrayW(_counter,0x8200,ref bytes,out uint count,IntPtr.Zero);
                if (result != 0x800007D2 || bytes == 0 || bytes > 32 * 1024 * 1024) return null;
                IntPtr buffer = Marshal.AllocHGlobal((int)bytes);
                try {
                    result = PdhGetFormattedCounterArrayW(_counter,0x8200,ref bytes,out count,buffer);
                    if (result == 0x800007D2) continue;
                    if (result != 0) return null;
                    var engines = new Dictionary<string,double>();
                    int stride = Marshal.SizeOf<CounterItem>();
                    if ((long)count * stride > bytes) return null;
                    for (int i = 0; i < count; i++) {
                        var item = Marshal.PtrToStructure<CounterItem>(IntPtr.Add(buffer,i * stride));
                        if (item.Value.Status > 1 || !double.IsFinite(item.Value.Number)) continue;
                        string name = Marshal.PtrToStringUni(item.Name) ?? "";
                        if (!name.Contains("luid_",StringComparison.Ordinal)) continue;
                        // Sum processes sharing the same adapter/engine, then select the busiest engine.
                        string key = Regex.Replace(name,@"^pid_\d+_","");
                        engines[key] = engines.GetValueOrDefault(key) + Math.Max(0,item.Value.Number);
                    }
                    if (engines.Count == 0) return null;
                    var busiest = engines.MaxBy(pair => pair.Value);
                    return (Math.Clamp(busiest.Value,0,100),busiest.Key);
                } finally { Marshal.FreeHGlobal(buffer); }
            }
        } catch { }
        return null;
    }
    public void Dispose() { if (_query != IntPtr.Zero) { PdhCloseQuery(_query); _query = IntPtr.Zero; _counter = IntPtr.Zero; } }
    [StructLayout(LayoutKind.Explicit,Size = 16)] private struct CounterValue { [FieldOffset(0)] public uint Status; [FieldOffset(8)] public double Number; }
    [StructLayout(LayoutKind.Sequential)] private struct CounterItem { public IntPtr Name; public CounterValue Value; }
    [DllImport("pdh.dll",CharSet = CharSet.Unicode)] private static extern uint PdhOpenQueryW(string? source,UIntPtr userData,out IntPtr query);
    [DllImport("pdh.dll",CharSet = CharSet.Unicode)] private static extern uint PdhAddEnglishCounterW(IntPtr query,string path,UIntPtr userData,out IntPtr counter);
    [DllImport("pdh.dll")] private static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll",CharSet = CharSet.Unicode)] private static extern uint PdhGetFormattedCounterArrayW(IntPtr counter,uint format,ref uint bytes,out uint count,IntPtr buffer);
    [DllImport("pdh.dll")] private static extern uint PdhCloseQuery(IntPtr query);
}
