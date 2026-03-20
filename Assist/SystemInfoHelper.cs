using System.Management;
using System.Runtime.InteropServices;

namespace Assist;

/// <summary>
/// Collects detailed system information — AIDA64-style.
/// </summary>
public static class SystemInfoHelper
{
    public sealed class SystemInfo
    {
        public string? OperatingSystem { get; set; }
        public string? OSVersion { get; set; }
        public string? DotNetVersion { get; set; }
        public string? ComputerName { get; set; }
        public string? UserName { get; set; }
        public string? ComputerManufacturer { get; set; }
        public string? ComputerModel { get; set; }
        public string? ProcessorInfo { get; set; }
        public string? ProcessorSpeed { get; set; }
        public string? RAM { get; set; }
        public List<string> RAMModules { get; set; } = [];
        public string? SystemUptime { get; set; }
        public string? Architecture { get; set; }
        public string? BIOS { get; set; }
        public string? BIOSVersion { get; set; }
        public List<string> StorageDevices { get; set; } = [];
        public List<string> GraphicsCards { get; set; } = [];
        public List<string> NetworkAdapters { get; set; } = [];
        public List<string> Monitors { get; set; } = [];
        public List<string> Sensors { get; set; } = [];
        public string? SystemInstallDate { get; set; }
    }

    /// <summary>Executes a WMI query and returns the first matching property value, or the fallback.</summary>
    private static string QueryWmiScalar(string query, string propertyName, string fallback = "Unknown")
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            foreach (var obj in searcher.Get())
                return obj[propertyName]?.ToString() ?? fallback;
        }
        catch { }
        return fallback;
    }

    public static SystemInfo GetSystemInfo() => new()
    {
        OperatingSystem      = GetOperatingSystem(),
        OSVersion            = Environment.OSVersion.ToString(),
        DotNetVersion        = RuntimeInformation.FrameworkDescription,
        ComputerName         = Environment.MachineName,
        UserName             = Environment.UserName,
        ProcessorInfo        = GetProcessorInfo(),
        ProcessorSpeed       = GetProcessorSpeed(),
        RAM                  = GetTotalRAM(),
        RAMModules           = GetRAMModules(),
        SystemUptime         = GetSystemUptime(),
        Architecture         = RuntimeInformation.OSArchitecture.ToString(),
        ComputerManufacturer = GetComputerManufacturer(),
        ComputerModel        = GetComputerModel(),
        BIOS                 = GetBIOSInfo(),
        BIOSVersion          = GetBIOSVersion(),
        StorageDevices       = GetStorageDevices(),
        GraphicsCards        = GetGraphicsCards(),
        NetworkAdapters      = GetNetworkAdapters(),
        Monitors             = GetMonitors(),
        SystemInstallDate    = GetSystemInstallDate()
    };

    private static string GetOperatingSystem()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "Windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))   return "Linux";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))     return "macOS";
        return "Unknown";
    }

    private static string GetProcessorInfo() =>
        QueryWmiScalar("select Name from Win32_Processor", "Name", $"Processors: {Environment.ProcessorCount}");

    private static string GetProcessorSpeed()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("select MaxClockSpeed from Win32_Processor");
            foreach (var obj in searcher.Get())
                if (int.TryParse(obj["MaxClockSpeed"]?.ToString(), out var mhz))
                    return $"{mhz / 1000.0:F2} GHz";
        }
        catch { }
        return "Unknown";
    }

    private static string GetTotalRAM()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("select TotalPhysicalMemory from Win32_ComputerSystem");
            foreach (var obj in searcher.Get())
                if (long.TryParse(obj["TotalPhysicalMemory"]?.ToString(), out var bytes))
                    return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
        catch { }
        return "Unknown";
    }

    private static List<string> GetRAMModules()
    {
        var modules = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "select Capacity, Manufacturer, Speed, MemoryType from Win32_PhysicalMemory");
            var index = 1;
            foreach (var obj in searcher.Get())
            {
                try
                {
                    if (!long.TryParse(obj["Capacity"]?.ToString(), out var bytes)) continue;
                    var manufacturer = obj["Manufacturer"]?.ToString() ?? "Unknown";
                    var speed        = obj["Speed"]?.ToString() ?? "Unknown";
                    var type         = GetMemoryType(obj["MemoryType"]?.ToString());
                    modules.Add($"Module {index++}: {bytes / (1024.0 * 1024.0 * 1024.0):F2} GB - {manufacturer} - {speed} MHz - {type}");
                }
                catch { }
            }
        }
        catch { }
        return modules;
    }

    private static string GetMemoryType(string? type) => type switch
    {
        "20" => "DDR",
        "21" => "DDR2",
        "24" => "DDR3",
        "26" => "DDR4",
        "34" => "DDR5",
        _    => "Unknown"
    };

    private static string GetSystemUptime()
    {
        try
        {
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
        }
        catch { }
        return "Unknown";
    }

    private static string GetComputerManufacturer() =>
        QueryWmiScalar("select Manufacturer from Win32_ComputerSystem", "Manufacturer");

    private static string GetComputerModel() =>
        QueryWmiScalar("select Model from Win32_ComputerSystem", "Model");

    private static string GetBIOSInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("select Manufacturer, Name from Win32_BIOS");
            foreach (var obj in searcher.Get())
                return $"{obj["Manufacturer"]?.ToString() ?? "Unknown"} {obj["Name"]?.ToString() ?? "Unknown"}";
        }
        catch { }
        return "Unknown";
    }

    private static string GetBIOSVersion()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "select SMBIOSBIOSVersion, ReleaseDate from Win32_BIOS");
            foreach (var obj in searcher.Get())
            {
                var version     = obj["SMBIOSBIOSVersion"]?.ToString() ?? "Unknown";
                var releaseDate = "Unknown";
                var raw         = obj["ReleaseDate"]?.ToString();
                if (!string.IsNullOrEmpty(raw))
                {
                    try   { releaseDate = ManagementDateTimeConverter.ToDateTime(raw).ToString("yyyy-MM-dd"); }
                    catch { releaseDate = raw; }
                }
                return $"Version: {version} - Release: {releaseDate}";
            }
        }
        catch { }
        return "Unknown";
    }

    private static List<string> GetStorageDevices()
    {
        var devices = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "select DeviceID, Model, Size, MediaType from Win32_DiskDrive");
            foreach (var obj in searcher.Get())
            {
                try
                {
                    var deviceId = obj["DeviceID"]?.ToString() ?? "Unknown";
                    var model    = obj["Model"]?.ToString() ?? "Unknown";
                    var media    = obj["MediaType"]?.ToString() ?? "Unknown";
                    var sizeStr  = "Unknown";
                    if (long.TryParse(obj["Size"]?.ToString(), out var bytes))
                    {
                        var gb = bytes / (1024.0 * 1024.0 * 1024.0);
                        sizeStr = gb > 1024 ? $"{gb / 1024:F2} TB" : $"{gb:F2} GB";
                    }
                    devices.Add($"{deviceId}: {model} - {sizeStr} - {media}");
                }
                catch { }
            }
        }
        catch { }
        return devices;
    }

    private static List<string> GetGraphicsCards()
    {
        var cards = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "select Name, AdapterRAM, DriverVersion, VideoProcessor from Win32_VideoController");
            foreach (var obj in searcher.Get())
            {
                try
                {
                    var name      = obj["Name"]?.ToString() ?? "Unknown";
                    var driver    = obj["DriverVersion"]?.ToString() ?? "Unknown";
                    var processor = obj["VideoProcessor"]?.ToString() ?? string.Empty;
                    var memStr    = "Unknown";
                    if (long.TryParse(obj["AdapterRAM"]?.ToString(), out var bytes))
                    {
                        var mb = bytes / (1024.0 * 1024.0);
                        memStr = mb >= 1024 ? $"{mb / 1024:F2} GB" : $"{mb:F0} MB";
                    }
                    var desc = string.IsNullOrEmpty(processor) ? name : $"{name} ({processor})";
                    cards.Add($"{desc} - Memory: {memStr} - Driver: {driver}");
                }
                catch { }
            }

            if (cards.Count == 0)
            {
                using var fallback = new ManagementObjectSearcher(
                    "select DeviceName, PelsWidth, PelsHeight from Win32_DisplayConfiguration");
                foreach (var obj in fallback.Get())
                {
                    try
                    {
                        var name = obj["DeviceName"]?.ToString() ?? "Display";
                        var w    = obj["PelsWidth"]?.ToString();
                        var h    = obj["PelsHeight"]?.ToString();
                        cards.Add(w is not null && h is not null ? $"{name} - {w}x{h}" : name);
                    }
                    catch { }
                }
            }

            if (cards.Count == 0)
            {
                using var pnp = new ManagementObjectSearcher(
                    "select Name, Manufacturer from Win32_PnPEntity where PNPClass='Display' or Name like '%NVIDIA%' or Name like '%AMD%' or Name like '%Intel%'");
                foreach (var obj in pnp.Get())
                {
                    try
                    {
                        var name = obj["Name"]?.ToString() ?? "Unknown GPU";
                        var man  = obj["Manufacturer"]?.ToString() ?? string.Empty;
                        cards.Add(!string.IsNullOrEmpty(man) ? $"{name} - {man}" : name);
                    }
                    catch { }
                }
            }
        }
        catch { }
        return cards;
    }

    private static List<string> GetNetworkAdapters()
    {
        var adapters = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "select Description, MACAddress, Speed from Win32_NetworkAdapter where PhysicalAdapter=True");
            foreach (var obj in searcher.Get())
            {
                try
                {
                    var description = obj["Description"]?.ToString() ?? "Unknown";
                    var mac         = obj["MACAddress"]?.ToString() ?? "Unknown";
                    var speedStr    = "Unknown";
                    if (long.TryParse(obj["Speed"]?.ToString(), out var bps) && bps > 0 && bps <= 10_000_000_000_000L)
                        speedStr = $"{bps / 1_000_000} Mbps";
                    adapters.Add($"{description} - MAC: {mac} - Speed: {speedStr}");
                }
                catch { }
            }
        }
        catch { }
        return adapters;
    }

    private static List<string> GetMonitors()
    {
        var monitors = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "select Name, ScreenHeight, ScreenWidth from Win32_DesktopMonitor");
            foreach (var obj in searcher.Get())
            {
                try
                {
                    var name = obj["Name"]?.ToString() ?? "Monitor";
                    var w    = obj["ScreenWidth"]?.ToString();
                    var h    = obj["ScreenHeight"]?.ToString();
                    if (w is not null && h is not null)
                        monitors.Add($"{name} - {w}x{h}");
                }
                catch { }
            }
        }
        catch { }
        return monitors;
    }

    private static string GetSystemInstallDate() =>
        QueryWmiScalar("select InstallDate from Win32_OperatingSystem", "InstallDate");
}
