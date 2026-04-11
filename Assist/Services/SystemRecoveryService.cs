namespace Assist.Services;

using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;

/// <summary>Log severity for recovery operations.</summary>
internal enum RecoveryLogLevel { Info, Success, Warning, Error }

/// <summary>Snapshot of current system hardware state.</summary>
internal sealed record SystemRecoveryInfo(
    int DisplayCount,
    List<string> BluetoothAudioDevices,
    string DefaultAudioDevice,
    string PowerStatus,
    bool IsAdmin);

/// <summary>
/// Safe hardware diagnostics and recovery operations.
/// No driver deletion, no registry modification, no physical hardware simulation.
/// </summary>
internal static class SystemRecoveryService
{
    // ── Admin Check ───────────────────────────────────────────

    public static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    // ── System Info ───────────────────────────────────────────

    public static async Task<SystemRecoveryInfo> GetSystemInfoAsync()
    {
        return await Task.Run(() =>
        {
            int displayCount = Screen.AllScreens.Length;
            var btDevices = GetBluetoothAudioDevices();
            string defaultAudio = GetDefaultAudioDevice();
            string power = GetPowerStatusText();
            bool isAdmin = IsRunningAsAdmin();
            return new SystemRecoveryInfo(displayCount, btDevices, defaultAudio, power, isAdmin);
        });
    }

    private static List<string> GetBluetoothAudioDevices()
    {
        var devices = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, DeviceID FROM Win32_PnPEntity WHERE PNPClass = 'AudioEndpoint'");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name)) continue;

                var deviceId = obj["DeviceID"]?.ToString() ?? "";
                if (deviceId.Contains("BTHENUM", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase))
                {
                    devices.Add(name);
                }
            }
        }
        catch { /* WMI unavailable */ }
        return devices;
    }

    private static string GetDefaultAudioDevice()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, Status FROM Win32_SoundDevice");
            foreach (ManagementObject obj in searcher.Get())
            {
                if ("OK".Equals(obj["Status"]?.ToString(), StringComparison.OrdinalIgnoreCase))
                    return obj["Name"]?.ToString() ?? "Bilinmiyor";
            }
        }
        catch { }
        return "Bilinmiyor";
    }

    private static string GetPowerStatusText()
    {
        var ps = SystemInformation.PowerStatus;
        string source = ps.PowerLineStatus switch
        {
            PowerLineStatus.Online => "AC (Prize Takılı)",
            PowerLineStatus.Offline => "DC (Pil)",
            _ => "Bilinmiyor"
        };
        string battery = ps.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery)
            ? "Pil Yok"
            : $"%{(int)(ps.BatteryLifePercent * 100)}";
        return $"{source} — {battery}";
    }

    // ── Audio Fix ─────────────────────────────────────────────

    public static async Task RunAudioFixAsync(Action<string, RecoveryLogLevel> log)
    {
        log("Audio Fix başlatılıyor...", RecoveryLogLevel.Info);

        if (!IsRunningAsAdmin())
            log("Admin yetkisi yok — servis işlemleri başarısız olabilir.", RecoveryLogLevel.Warning);

        await Task.Run(() =>
        {
            // 1) Scan audio devices via WMI
            log("Ses cihazları taranıyor...", RecoveryLogLevel.Info);
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name, Status FROM Win32_SoundDevice");
                int count = 0;
                foreach (ManagementObject obj in searcher.Get())
                {
                    log($"  Cihaz: {obj["Name"] ?? "—"} | Durum: {obj["Status"] ?? "—"}", RecoveryLogLevel.Info);
                    count++;
                }
                log($"Toplam {count} ses cihazı bulundu.", count > 0 ? RecoveryLogLevel.Success : RecoveryLogLevel.Warning);
            }
            catch (Exception ex)
            {
                log($"Ses cihazları taranırken hata: {ex.Message}", RecoveryLogLevel.Error);
            }

            // 2) Scan audio endpoints
            log("Audio endpoint'ler kontrol ediliyor...", RecoveryLogLevel.Info);
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, Status FROM Win32_PnPEntity WHERE PNPClass = 'AudioEndpoint'");
                int count = 0;
                foreach (ManagementObject obj in searcher.Get())
                {
                    log($"  Endpoint: {obj["Name"] ?? "—"} | Durum: {obj["Status"] ?? "—"}", RecoveryLogLevel.Info);
                    count++;
                }
                log($"Toplam {count} audio endpoint bulundu.", count > 0 ? RecoveryLogLevel.Success : RecoveryLogLevel.Warning);
            }
            catch (Exception ex)
            {
                log($"Audio endpoint taraması başarısız: {ex.Message}", RecoveryLogLevel.Error);
            }

            // 3) Check and safe-reset Windows Audio services
            string[] services = ["Audiosrv", "AudioEndpointBuilder"];
            foreach (var svcName in services)
            {
                log($"Servis kontrol ediliyor: {svcName}...", RecoveryLogLevel.Info);
                try
                {
                    string state = GetServiceState(svcName);
                    log($"  {svcName} durumu: {state}", RecoveryLogLevel.Info);

                    if (state == "NotFound")
                    {
                        log($"  {svcName} servisi bulunamadı.", RecoveryLogLevel.Warning);
                        continue;
                    }

                    if (!state.Equals("Running", StringComparison.OrdinalIgnoreCase))
                    {
                        log($"  {svcName} çalışmıyor, başlatılıyor...", RecoveryLogLevel.Warning);
                        bool ok = StartWmiService(svcName);
                        log(ok ? $"  {svcName} başarıyla başlatıldı." : $"  {svcName} başlatılamadı.",
                            ok ? RecoveryLogLevel.Success : RecoveryLogLevel.Error);
                    }
                    else
                    {
                        log($"  {svcName} safe reset yapılıyor...", RecoveryLogLevel.Info);
                        StopWmiService(svcName);
                        Thread.Sleep(800);
                        bool ok = StartWmiService(svcName);
                        log(ok ? $"  {svcName} yeniden başlatıldı." : $"  {svcName} yeniden başlatılamadı.",
                            ok ? RecoveryLogLevel.Success : RecoveryLogLevel.Error);
                    }
                }
                catch (Exception ex)
                {
                    log($"  {svcName} işlemi başarısız: {ex.Message}", RecoveryLogLevel.Error);
                }
            }
        });

        log("Audio Fix tamamlandı.", RecoveryLogLevel.Success);
    }

    // ── Display Fix ───────────────────────────────────────────

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum,
        ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int ChangeDisplaySettingsEx(string? lpszDeviceName,
        IntPtr lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    public static async Task RunDisplayFixAsync(Action<string, RecoveryLogLevel> log)
    {
        log("Display Fix başlatılıyor...", RecoveryLogLevel.Info);

        await Task.Run(() =>
        {
            // 1) List screens via .NET API
            log("Ekranlar listeleniyor...", RecoveryLogLevel.Info);
            var screens = Screen.AllScreens;
            foreach (var s in screens)
                log($"  {s.DeviceName} — {s.Bounds.Width}x{s.Bounds.Height} | Primary: {s.Primary}", RecoveryLogLevel.Info);
            log($"Toplam {screens.Length} ekran algılandı.", screens.Length > 0 ? RecoveryLogLevel.Success : RecoveryLogLevel.Warning);

            // 2) Enumerate display devices via P/Invoke
            log("Display device'lar taranıyor...", RecoveryLogLevel.Info);
            uint idx = 0;
            int active = 0;
            var dd = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
            while (EnumDisplayDevices(null, idx, ref dd, 0))
            {
                bool isActive = (dd.StateFlags & 0x1) != 0;
                log($"  [{idx}] {dd.DeviceName} — {dd.DeviceString} | {(isActive ? "AKTİF" : "PASİF")}", RecoveryLogLevel.Info);
                if (isActive) active++;
                dd.cb = Marshal.SizeOf<DISPLAY_DEVICE>();
                idx++;
            }
            log($"{idx} display device, {active} aktif.", active > 0 ? RecoveryLogLevel.Success : RecoveryLogLevel.Warning);

            // 3) WMI monitor query
            log("WMI monitor bilgileri sorgulanıyor...", RecoveryLogLevel.Info);
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name, Status FROM Win32_DesktopMonitor");
                int wmiCount = 0;
                foreach (ManagementObject obj in searcher.Get())
                {
                    log($"  WMI: {obj["Name"] ?? "—"} | Durum: {obj["Status"] ?? "—"}", RecoveryLogLevel.Info);
                    wmiCount++;
                }
                log($"WMI: {wmiCount} monitor bulundu.", wmiCount > 0 ? RecoveryLogLevel.Success : RecoveryLogLevel.Warning);
            }
            catch (Exception ex)
            {
                log($"WMI monitor sorgusu başarısız: {ex.Message}", RecoveryLogLevel.Error);
            }

            // 4) Safe display config refresh (resets to registry-stored settings)
            log("Display config refresh yapılıyor...", RecoveryLogLevel.Info);
            try
            {
                int result = ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
                log(result == 0
                    ? "Display config refresh başarılı."
                    : $"Display config refresh kodu: {result}",
                    result == 0 ? RecoveryLogLevel.Success : RecoveryLogLevel.Warning);
            }
            catch (Exception ex)
            {
                log($"Display config refresh başarısız: {ex.Message}", RecoveryLogLevel.Error);
            }
        });

        log("Display Fix tamamlandı.", RecoveryLogLevel.Success);
    }

    // ── Power Refresh ─────────────────────────────────────────

    public static async Task RunPowerRefreshAsync(Action<string, RecoveryLogLevel> log)
    {
        log("Power Refresh başlatılıyor...", RecoveryLogLevel.Info);

        await Task.Run(() =>
        {
            // 1) Query .NET PowerStatus
            log("Güç durumu sorgulanıyor...", RecoveryLogLevel.Info);
            var ps = SystemInformation.PowerStatus;
            string src = ps.PowerLineStatus switch
            {
                PowerLineStatus.Online => "AC (Prize Takılı)",
                PowerLineStatus.Offline => "DC (Pil)",
                _ => "Bilinmiyor"
            };
            log($"  Güç Kaynağı: {src}", RecoveryLogLevel.Info);

            if (!ps.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery))
            {
                log($"  Pil Seviyesi: %{(int)(ps.BatteryLifePercent * 100)}", RecoveryLogLevel.Info);
                log($"  Pil Durumu: {ps.BatteryChargeStatus}", RecoveryLogLevel.Info);
                if (ps.BatteryLifeRemaining > 0)
                    log($"  Kalan Süre: {TimeSpan.FromSeconds(ps.BatteryLifeRemaining):hh\\:mm\\:ss}", RecoveryLogLevel.Info);
            }
            else
            {
                log("  Sistemde pil algılanmadı.", RecoveryLogLevel.Info);
            }

            // 2) WMI Battery detail
            log("WMI pil bilgileri sorgulanıyor...", RecoveryLogLevel.Info);
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
                int count = 0;
                foreach (ManagementObject obj in searcher.Get())
                {
                    log($"  WMI: {obj["Name"] ?? "—"} | Şarj: %{obj["EstimatedChargeRemaining"] ?? "—"} | BatteryStatus: {obj["BatteryStatus"] ?? "—"}", RecoveryLogLevel.Info);
                    count++;
                }
                log(count > 0 ? $"WMI: {count} pil algılandı." : "WMI: Pil bulunamadı.",
                    count > 0 ? RecoveryLogLevel.Success : RecoveryLogLevel.Info);
            }
            catch (Exception ex)
            {
                log($"WMI pil sorgusu başarısız: {ex.Message}", RecoveryLogLevel.Error);
            }

            // 3) Re-query power state for refresh
            log("Güç durumu yeniden sorgulanıyor...", RecoveryLogLevel.Info);
            var ps2 = SystemInformation.PowerStatus;
            if (ps2.PowerLineStatus == PowerLineStatus.Unknown)
            {
                log("Güç durumu belirlenemedi. Fiziksel bağlantıyı kontrol edin.", RecoveryLogLevel.Warning);
                log("Bu sorun donanım kaynaklı olabilir — yazılımsal çözüm mümkün değil.", RecoveryLogLevel.Warning);
            }
            else
            {
                string srcAfter = ps2.PowerLineStatus == PowerLineStatus.Online
                    ? "AC (Prize Takılı)" : "DC (Pil)";
                string batAfter = ps2.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery)
                    ? "Pil Yok" : $"%{(int)(ps2.BatteryLifePercent * 100)}";
                log($"Güncel Durum: {srcAfter} — {batAfter}", RecoveryLogLevel.Success);
            }
        });

        log("Power Refresh tamamlandı.", RecoveryLogLevel.Success);
    }

    // ── WMI Service Helpers ───────────────────────────────────

    private static string GetServiceState(string serviceName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT State FROM Win32_Service WHERE Name = '{serviceName}'");
            foreach (ManagementObject obj in searcher.Get())
                return obj["State"]?.ToString() ?? "Unknown";
        }
        catch { }
        return "NotFound";
    }

    private static bool StartWmiService(string serviceName)
    {
        try
        {
            using var svc = new ManagementObject($"Win32_Service.Name='{serviceName}'");
            svc.Get();
            var result = svc.InvokeMethod("StartService", null);
            return result?.ToString() == "0";
        }
        catch { return false; }
    }

    private static bool StopWmiService(string serviceName)
    {
        try
        {
            using var svc = new ManagementObject($"Win32_Service.Name='{serviceName}'");
            svc.Get();
            var result = svc.InvokeMethod("StopService", null);
            return result?.ToString() == "0";
        }
        catch { return false; }
    }
}
