using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace LaptopBar;
public static class Connections
{
    public static (bool connected, string detail) Wifi()
    {
        try { var adapter = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 && n.OperationalStatus == OperationalStatus.Up); return (adapter != null, adapter?.Name ?? "연결 안 됨"); }
        catch { return (false, "상태 확인 불가"); }
    }
    public static (bool enabled, string detail) Bluetooth()
    {
        var find = new RadioParams { Size = (uint)Marshal.SizeOf<RadioParams>() };
        IntPtr search = BluetoothFindFirstRadio(ref find, out IntPtr radio);
        if (search == IntPtr.Zero) return (false, "라디오 없음 / 꺼짐");
        try
        {
            var args = new DeviceSearch { Size = (uint)Marshal.SizeOf<DeviceSearch>(), Connected = true, Radio = radio };
            var device = new DeviceInfo { Size = (uint)Marshal.SizeOf<DeviceInfo>() };
            IntPtr devices = BluetoothFindFirstDevice(ref args, ref device);
            if (devices == IntPtr.Zero) return (true, "라디오 사용 가능");
            try { return (true, string.IsNullOrWhiteSpace(device.Name) ? "연결된 기기 있음" : device.Name); }
            finally { BluetoothFindDeviceClose(devices); }
        }
        finally { CloseHandle(radio); BluetoothFindRadioClose(search); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct RadioParams { public uint Size; }
    [StructLayout(LayoutKind.Sequential)] private struct DeviceSearch { public uint Size; [MarshalAs(UnmanagedType.Bool)] public bool Authenticated; [MarshalAs(UnmanagedType.Bool)] public bool Remembered; [MarshalAs(UnmanagedType.Bool)] public bool Unknown; [MarshalAs(UnmanagedType.Bool)] public bool Connected; [MarshalAs(UnmanagedType.Bool)] public bool Inquiry; public byte Timeout; public IntPtr Radio; }
    [StructLayout(LayoutKind.Sequential)] private struct SystemTime { public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct DeviceInfo { public uint Size; public ulong Address; public uint Class; [MarshalAs(UnmanagedType.Bool)] public bool Connected; [MarshalAs(UnmanagedType.Bool)] public bool Remembered; [MarshalAs(UnmanagedType.Bool)] public bool Authenticated; public SystemTime Seen, Used; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)] public string Name; }
    [DllImport("bthprops.cpl")] private static extern IntPtr BluetoothFindFirstRadio(ref RadioParams args, out IntPtr radio);
    [DllImport("bthprops.cpl")] private static extern bool BluetoothFindRadioClose(IntPtr find);
    [DllImport("bthprops.cpl", CharSet = CharSet.Unicode)] private static extern IntPtr BluetoothFindFirstDevice(ref DeviceSearch args, ref DeviceInfo device);
    [DllImport("bthprops.cpl")] private static extern bool BluetoothFindDeviceClose(IntPtr find);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
}

// Windows Core Audio default endpoint. COM objects are released on every access,
// so switching headphones / Bluetooth devices is picked up on the next refresh.
public static class Audio
{
    public static (bool available, double volume, bool muted) Read(bool microphone)
    {
        try { return WithEndpoint(microphone, endpoint => { Marshal.ThrowExceptionForHR(endpoint.GetMasterVolumeLevelScalar(out float v)); Marshal.ThrowExceptionForHR(endpoint.GetMute(out bool m)); return (true, (double)v * 100, m); }); }
        catch { return (false, 0, false); }
    }
    public static void Set(bool microphone, double volume) => WithEndpoint(microphone, endpoint => { Marshal.ThrowExceptionForHR(endpoint.SetMasterVolumeLevelScalar((float)(volume / 100), Guid.Empty)); return true; });
    public static void ToggleMute(bool microphone) => WithEndpoint(microphone, endpoint => { Marshal.ThrowExceptionForHR(endpoint.GetMute(out bool mute)); Marshal.ThrowExceptionForHR(endpoint.SetMute(!mute, Guid.Empty)); return true; });
    private static T WithEndpoint<T>(bool microphone, Func<IAudioEndpointVolume,T> action)
    {
        object enumeratorObject = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"))!)!;
        IMMDevice? device = null; object? endpointObject = null;
        try
        {
            var enumerator = (IMMDeviceEnumerator)enumeratorObject;
            Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(microphone ? 1 : 0, 1, out device));
            var iid = typeof(IAudioEndpointVolume).GUID;
            Marshal.ThrowExceptionForHR(device.Activate(ref iid, 23, IntPtr.Zero, out endpointObject));
            return action((IAudioEndpointVolume)endpointObject);
        }
        finally { if (endpointObject != null) Marshal.ReleaseComObject(endpointObject); if (device != null) Marshal.ReleaseComObject(device); Marshal.ReleaseComObject(enumeratorObject); }
    }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IMMDeviceEnumerator
    { [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out IntPtr devices); [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device); }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IMMDevice
    { [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr activation, [MarshalAs(UnmanagedType.IUnknown)] out object instance); }
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float level, Guid context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, Guid context);
        [PreserveSig] int GetMasterVolumeLevel(out float level);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, Guid context);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, Guid context);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
