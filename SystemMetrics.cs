using System;
using System.Runtime.InteropServices;

namespace LaptopBar;
public sealed class SystemMetrics
{
    private ulong _idle, _total;
    public double Cpu { get; private set; }
    public double UsedGb { get; private set; }
    public double TotalGb { get; private set; }
    public uint RamPercent { get; private set; }
    public string Battery { get; private set; } = "—";
    public string PowerDetail { get; private set; } = "전원 상태 확인 중";
    public void Update()
    {
        if (GetSystemTimes(out var idle, out var kernel, out var user))
        {
            ulong total = kernel + user;
            if (_total != 0 && total > _total) Cpu = Math.Clamp(100.0 * (1 - (double)(idle - _idle) / (total - _total)), 0, 100);
            _idle = idle; _total = total;
        }
        var mem = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (GlobalMemoryStatusEx(ref mem))
        {
            TotalGb = mem.TotalPhysical / 1073741824.0;
            UsedGb = (mem.TotalPhysical - mem.AvailablePhysical) / 1073741824.0;
            RamPercent = mem.Load;
        }
        if (GetSystemPowerStatus(out var p))
        {
            bool absent = (p.BatteryFlag & 128) != 0;
            Battery = absent ? "AC" : p.Percent == 255 ? "—" : $"{p.Percent}%";
            PowerDetail = absent ? "배터리가 없는 장치" : p.AcLine == 1 ? "전원 연결됨" : "배터리 사용 중";
            if (!absent && p.AcLine == 0 && p.LifeTime != uint.MaxValue) PowerDetail += $" · 약 {p.LifeTime / 3600}시간 {p.LifeTime % 3600 / 60}분";
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus { public uint Length, Load; public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual; }
    [StructLayout(LayoutKind.Sequential)] private struct PowerStatus { public byte AcLine, BatteryFlag, Percent, Reserved; public uint LifeTime, FullLifeTime; }
    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("kernel32.dll")] private static extern bool GetSystemPowerStatus(out PowerStatus status);
}
