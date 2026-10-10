using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Timzoner.Core;

namespace Timzoner;

public sealed class WindowsTimeZoneService : ITimeZoneService
{
    public IReadOnlyList<Zone> GetZones() => TimeZoneInfo.GetSystemTimeZones().Select(z => new Zone(z.Id, z.DisplayName, z.BaseUtcOffset)).ToArray();

    public string GetCurrentId()
    {
        if (Native.GetDynamicTimeZoneInformation(out var info) == uint.MaxValue) throw new Win32Exception(Marshal.GetLastWin32Error());
        return info.TimeZoneKeyName;
    }

    public static (string Id, bool DaylightDisabled) GetSnapshot()
    {
        if (Native.GetDynamicTimeZoneInformation(out var info) == uint.MaxValue) throw new Win32Exception(Marshal.GetLastWin32Error());
        return (info.TimeZoneKeyName, info.DynamicDaylightTimeDisabled != 0);
    }

    public bool? IsAutomaticEnabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\tzautoupdate");
            return key?.GetValue("Start") is int start ? start == 3 : null;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException) { return null; }
    }

    public Task<SwitchResult> SetAsync(string id, bool elevated) => elevated ? ElevateAsync(id) : Task.Run(() => SetDirect(id));

    public static SwitchResult SetDirect(string id, bool daylightDisabled = false)
    {
        Native.DynamicTimeZone target = default;
        bool found = false;
        for (uint index = 0; ; index++)
        {
            var code = Native.EnumDynamicTimeZoneInformation(index, out var zone);
            if (code == 259) break;
            if (code != 0) return Error((int)code);
            if (!string.Equals(zone.TimeZoneKeyName, id, StringComparison.OrdinalIgnoreCase)) continue;
            target = zone;
            found = true;
            break;
        }
        if (!found) return new(SwitchStatus.InvalidZone, AppText.Get("时区 ID 无效。", "The time zone ID is invalid."));
        if (!Native.OpenProcessToken(Native.GetCurrentProcess(), 0x20 | 0x8, out var token)) return Error(Marshal.GetLastWin32Error());
        using (token)
        {
            if (!Native.LookupPrivilegeValue(null, "SeTimeZonePrivilege", out var luid)) return Error(Marshal.GetLastWin32Error());
            var desired = new Native.TokenPrivileges { Count = 1, Luid = luid, Attributes = 2 };
            if (!Native.AdjustTokenPrivileges(token, false, ref desired, (uint)Marshal.SizeOf<Native.TokenPrivileges>(), out var previous, out _))
                return Error(Marshal.GetLastWin32Error());
            int privilegeError = Marshal.GetLastWin32Error();
            if (privilegeError != 0) return Error(privilegeError);
            try
            {
                target.DynamicDaylightTimeDisabled = daylightDisabled ? (byte)1 : (byte)0;
                if (!Native.SetDynamicTimeZoneInformation(ref target)) return Error(Marshal.GetLastWin32Error());
            }
            finally { Native.AdjustTokenPrivileges(token, false, ref previous, 0, out _, out _); }
        }
        // Notify other desktop applications without waiting indefinitely on a hung window.
        Native.SendMessageTimeout(new IntPtr(0xffff), 0x001a, IntPtr.Zero, "TimeZoneInformation", 2, 1000, out _);
        Native.SendMessageTimeout(new IntPtr(0xffff), 0x001e, IntPtr.Zero, null, 2, 1000, out _);
        return new(SwitchStatus.Success, AppText.Get("系统时区已切换。", "The system time zone has been changed."));
    }

    private static async Task<SwitchResult> ElevateAsync(string id)
    {
        try
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" };
            start.ArgumentList.Add("--elevated-switch");
            start.ArgumentList.Add(id);
            using var process = Process.Start(start);
            if (process is null) return new(SwitchStatus.Failed, AppText.Get("无法启动授权进程。", "Unable to start the authorization process."));
            await process.WaitForExitAsync();
            return process.ExitCode == 0 ? new(SwitchStatus.Success, AppText.Get("授权操作已完成。", "The authorized operation has completed.")) : Error(process.ExitCode);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return new(SwitchStatus.Cancelled, AppText.Get("已取消授权，系统时区未由本次操作修改。", "Authorization was cancelled. This operation did not change the system time zone.")); }
        catch (Win32Exception ex) { return Error(ex.NativeErrorCode); }
    }

    private static SwitchResult Error(int code) => new(code is 5 or 1300 or 1314 ? SwitchStatus.PermissionDenied : SwitchStatus.Failed,
        code is 5 or 1300 or 1314 ? AppText.Get("当前账户缺少修改时区的权限，可选择授权重试。", "Your account does not have permission to change the time zone. You can authorize a retry.")
            : AppText.Get("Windows 未完成切换。", "Windows did not complete the time zone change. ") + new Win32Exception(code).Message);
}

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct SystemTime { public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DynamicTimeZone
    {
        public int Bias;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string StandardName;
        public SystemTime StandardDate;
        public int StandardBias;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DaylightName;
        public SystemTime DaylightDate;
        public int DaylightBias;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string TimeZoneKeyName;
        public byte DynamicDaylightTimeDisabled;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct Luid { public uint Low; public int High; }
    [StructLayout(LayoutKind.Sequential)] internal struct TokenPrivileges { public uint Count; public Luid Luid; public uint Attributes; }

    [DllImport("kernel32.dll", SetLastError = true)] internal static extern uint GetDynamicTimeZoneInformation(out DynamicTimeZone info);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] internal static extern uint EnumDynamicTimeZoneInformation(uint index, out DynamicTimeZone info);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetDynamicTimeZoneInformation(ref DynamicTimeZone info);
    [DllImport("kernel32.dll")] internal static extern IntPtr GetCurrentProcess();
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool OpenProcessToken(IntPtr process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool LookupPrivilegeValue(string? system, string name, out Luid luid);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool AdjustTokenPrivileges(SafeAccessTokenHandle token, [MarshalAs(UnmanagedType.Bool)] bool disable, ref TokenPrivileges desired, uint length, out TokenPrivileges previous, out uint returnedLength);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wparam, string? lparam, uint flags, uint timeout, out IntPtr result);
}
