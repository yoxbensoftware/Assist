namespace Assist.Services;

using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Assist.Models;
using Microsoft.Win32;

internal interface IPowerGuardService
{
    bool IsActive { get; }
    bool Start();
    void Stop();
}

internal interface IPowerSettingsService
{
    bool BackupExists { get; }
    bool IsAdministrator();
    Task<PowerSettingsSnapshot> GetCurrentSettingsAsync(CancellationToken cancellationToken = default);
    Task<PowerSettingsOperationResult> ApplyPersistentNoSleepAsync(bool includeBatteryProfile, CancellationToken cancellationToken = default);
    Task<PowerSettingsOperationResult> RestoreFromBackupAsync(CancellationToken cancellationToken = default);
}

internal interface ISystemHealthMonitor
{
    Task<SystemHealthSnapshot> GetSnapshotAsync(
        bool guardActive,
        HeartbeatState heartbeat,
        CancellationToken cancellationToken = default);
}

internal interface INotificationService
{
    HeartbeatState Heartbeat { get; }
    Task<NotificationResult> SendTestAsync(CancellationToken cancellationToken = default);
    Task<NotificationResult> SendAlertAsync(
        string alertKey,
        string title,
        string message,
        GuardianSeverity severity,
        CancellationToken cancellationToken = default);
    Task<NotificationResult> SendHeartbeatAsync(SystemHealthSnapshot snapshot, CancellationToken cancellationToken = default);
}

internal interface IGuardianConfigService
{
    string ConfigFilePath { get; }
    GuardianConfig Current { get; }
    GuardianConfig Load();
    void Save();
    string GetMaskedRemoteSummary();
    string MaskSecrets(string value);
}

internal interface IGuardianLogService
{
    string LogFilePath { get; }
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception? exception = null);
}

internal static class NoSleepGuardianServiceFactory
{
    public static IGuardianLogService CreateLogService() => new GuardianLogService();

    public static IGuardianConfigService CreateConfigService(IGuardianLogService logService)
    {
        var configService = new GuardianConfigService(logService);
        configService.Load();
        return configService;
    }

    public static IPowerGuardService CreatePowerGuardService(IGuardianLogService logService) =>
        new PowerGuardService(logService);

    public static IPowerSettingsService CreatePowerSettingsService(IGuardianLogService logService) =>
        new PowerSettingsService(logService);

    public static INotificationService CreateNotificationService(
        IGuardianConfigService configService,
        IGuardianLogService logService) =>
        new NotificationService(configService, logService);

    public static ISystemHealthMonitor CreateHealthMonitor(
        IGuardianConfigService configService,
        IPowerSettingsService powerSettingsService,
        IGuardianLogService logService) =>
        new SystemHealthMonitor(configService, powerSettingsService, logService);
}

internal sealed class PowerGuardService(IGuardianLogService logService) : IPowerGuardService
{
    [Flags]
    private enum ExecutionState : uint
    {
        EsSystemRequired = 0x00000001,
        EsContinuous = 0x80000000,
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

    public bool IsActive { get; private set; }

    public bool Start()
    {
        var result = SetThreadExecutionState(ExecutionState.EsContinuous | ExecutionState.EsSystemRequired);
        if (result == 0)
        {
            logService.Error("SetThreadExecutionState failed while starting NoSleep guard.");
            IsActive = false;
            return false;
        }

        IsActive = true;
        logService.Info("Process-based NoSleep guard started.");
        return true;
    }

    public void Stop()
    {
        var result = SetThreadExecutionState(ExecutionState.EsContinuous);
        if (result == 0)
            logService.Error("SetThreadExecutionState failed while stopping NoSleep guard.");

        IsActive = false;
        logService.Info("Process-based NoSleep guard stopped.");
    }
}

internal sealed class PowerSettingsService(IGuardianLogService logService) : IPowerSettingsService
{
    private static readonly Guid SleepSubgroup = Guid.Parse("238c9fa8-0aad-41ed-83f4-97be242c8f20");
    private static readonly Guid StandbyIdleSetting = Guid.Parse("29f6c1db-86da-48c5-9fdb-f2b67b1f44da");
    private static readonly Guid HibernateIdleSetting = Guid.Parse("9d7815a6-7ee4-497e-8888-515a05f02364");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string BackupFilePath = Path.Combine(AppConstants.AppDataPath, "nosleep-guardian-power-backup.json");

    public bool BackupExists => File.Exists(BackupFilePath);

    public bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public Task<PowerSettingsSnapshot> GetCurrentSettingsAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var activeScheme = GetActiveScheme();
            var name = TryGetActiveSchemeName(activeScheme);
            return new PowerSettingsSnapshot(
                activeScheme,
                name,
                ReadValue(activeScheme, StandbyIdleSetting, ac: true),
                ReadValue(activeScheme, StandbyIdleSetting, ac: false),
                ReadValue(activeScheme, HibernateIdleSetting, ac: true),
                ReadValue(activeScheme, HibernateIdleSetting, ac: false),
                BackupExists);
        }, cancellationToken);

    public async Task<PowerSettingsOperationResult> ApplyPersistentNoSleepAsync(
        bool includeBatteryProfile,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdministrator())
            return new PowerSettingsOperationResult(false, "Bu işlem yönetici yetkisi gerektirir.");

        try
        {
            var snapshot = await GetCurrentSettingsAsync(cancellationToken).ConfigureAwait(false);
            EnsureBackup(snapshot);

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var scheme = snapshot.ActiveSchemeId;

                WriteValue(scheme, StandbyIdleSetting, ac: true, seconds: 0);
                WriteValue(scheme, HibernateIdleSetting, ac: true, seconds: 0);

                if (includeBatteryProfile)
                {
                    WriteValue(scheme, StandbyIdleSetting, ac: false, seconds: 0);
                    WriteValue(scheme, HibernateIdleSetting, ac: false, seconds: 0);
                }

                SetActiveScheme(scheme);
            }, cancellationToken).ConfigureAwait(false);

            var profile = includeBatteryProfile ? "AC ve batarya" : "AC";
            logService.Warning($"Persistent NoSleep mode applied for {profile} profile.");
            return new PowerSettingsOperationResult(true, $"Kalıcı uykusuz mod uygulandı ({profile}).");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logService.Error("Persistent NoSleep mode failed.", ex);
            return new PowerSettingsOperationResult(false, "Kalıcı güç ayarları uygulanamadı. Log dosyasını kontrol edin.");
        }
    }

    public async Task<PowerSettingsOperationResult> RestoreFromBackupAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAdministrator())
            return new PowerSettingsOperationResult(false, "Bu işlem yönetici yetkisi gerektirir.");

        if (!File.Exists(BackupFilePath))
            return new PowerSettingsOperationResult(false, "Geri alınacak güç ayarı yedeği bulunamadı.");

        try
        {
            var json = await File.ReadAllTextAsync(BackupFilePath, cancellationToken).ConfigureAwait(false);
            var backup = JsonSerializer.Deserialize<PowerSettingsBackup>(json);
            if (backup is null)
                return new PowerSettingsOperationResult(false, "Güç ayarı yedeği okunamadı.");

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteValue(backup.ActiveSchemeId, StandbyIdleSetting, ac: true, backup.SleepAcSeconds);
                WriteValue(backup.ActiveSchemeId, StandbyIdleSetting, ac: false, backup.SleepDcSeconds);
                WriteValue(backup.ActiveSchemeId, HibernateIdleSetting, ac: true, backup.HibernateAcSeconds);
                WriteValue(backup.ActiveSchemeId, HibernateIdleSetting, ac: false, backup.HibernateDcSeconds);
                SetActiveScheme(backup.ActiveSchemeId);
            }, cancellationToken).ConfigureAwait(false);

            File.Delete(BackupFilePath);
            logService.Warning("Power settings restored from NoSleep Guardian backup.");
            return new PowerSettingsOperationResult(true, "Güç ayarları yedekten geri alındı.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logService.Error("Power settings restore failed.", ex);
            return new PowerSettingsOperationResult(false, "Güç ayarları geri alınamadı. Log dosyasını kontrol edin.");
        }
    }

    private static void EnsureBackup(PowerSettingsSnapshot snapshot)
    {
        if (File.Exists(BackupFilePath))
            return;

        Directory.CreateDirectory(AppConstants.AppDataPath);
        var backup = new PowerSettingsBackup(
            snapshot.ActiveSchemeId,
            DateTime.UtcNow,
            snapshot.SleepAcSeconds,
            snapshot.SleepDcSeconds,
            snapshot.HibernateAcSeconds,
            snapshot.HibernateDcSeconds);

        File.WriteAllText(BackupFilePath, JsonSerializer.Serialize(backup, JsonOptions));
    }

    private static Guid GetActiveScheme()
    {
        var result = PowerGetActiveScheme(IntPtr.Zero, out var schemePtr);
        if (result != 0)
            throw new Win32Exception((int)result, "Active power scheme could not be read.");

        try
        {
            return Marshal.PtrToStructure<Guid>(schemePtr);
        }
        finally
        {
            LocalFree(schemePtr);
        }
    }

    private string TryGetActiveSchemeName(Guid scheme)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo("powercfg", "/getactivescheme")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(2500);

            var start = output.LastIndexOf('(');
            var end = output.LastIndexOf(')');
            if (start >= 0 && end > start)
                return output[(start + 1)..end].Trim();
        }
        catch
        {
            logService.Warning("Active power scheme name could not be read.");
        }

        return scheme.ToString();
    }

    private static uint ReadValue(Guid scheme, Guid setting, bool ac)
    {
        var schemeLocal = scheme;
        var subgroup = SleepSubgroup;
        var settingLocal = setting;
        var result = ac
            ? PowerReadACValueIndex(IntPtr.Zero, ref schemeLocal, ref subgroup, ref settingLocal, out var value)
            : PowerReadDCValueIndex(IntPtr.Zero, ref schemeLocal, ref subgroup, ref settingLocal, out value);

        if (result != 0)
            throw new Win32Exception((int)result, "Power setting could not be read.");

        return value;
    }

    private static void WriteValue(Guid scheme, Guid setting, bool ac, uint seconds)
    {
        var schemeLocal = scheme;
        var subgroup = SleepSubgroup;
        var settingLocal = setting;
        var result = ac
            ? PowerWriteACValueIndex(IntPtr.Zero, ref schemeLocal, ref subgroup, ref settingLocal, seconds)
            : PowerWriteDCValueIndex(IntPtr.Zero, ref schemeLocal, ref subgroup, ref settingLocal, seconds);

        if (result != 0)
            throw new Win32Exception((int)result, "Power setting could not be written.");
    }

    private static void SetActiveScheme(Guid scheme)
    {
        var schemeLocal = scheme;
        var result = PowerSetActiveScheme(IntPtr.Zero, ref schemeLocal);
        if (result != 0)
            throw new Win32Exception((int)result, "Power scheme could not be activated.");
    }

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint PowerReadACValueIndex(
        IntPtr rootPowerKey,
        ref Guid schemeGuid,
        ref Guid subgroupOfPowerSettingsGuid,
        ref Guid powerSettingGuid,
        out uint acValueIndex);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint PowerReadDCValueIndex(
        IntPtr rootPowerKey,
        ref Guid schemeGuid,
        ref Guid subgroupOfPowerSettingsGuid,
        ref Guid powerSettingGuid,
        out uint dcValueIndex);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint PowerWriteACValueIndex(
        IntPtr rootPowerKey,
        ref Guid schemeGuid,
        ref Guid subgroupOfPowerSettingsGuid,
        ref Guid powerSettingGuid,
        uint acValueIndex);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint PowerWriteDCValueIndex(
        IntPtr rootPowerKey,
        ref Guid schemeGuid,
        ref Guid subgroupOfPowerSettingsGuid,
        ref Guid powerSettingGuid,
        uint dcValueIndex);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);
}

internal sealed class SystemHealthMonitor(
    IGuardianConfigService configService,
    IPowerSettingsService powerSettingsService,
    IGuardianLogService logService) : ISystemHealthMonitor
{
    public async Task<SystemHealthSnapshot> GetSnapshotAsync(
        bool guardActive,
        HeartbeatState heartbeat,
        CancellationToken cancellationToken = default)
    {
        var config = configService.Current;
        var issues = new List<SystemHealthIssue>();
        var powerStatus = SystemInformation.PowerStatus;
        var hasBattery = powerStatus.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery;
        var batteryPercent = hasBattery ? Math.Clamp((int)Math.Round(powerStatus.BatteryLifePercent * 100), 0, 100) : (int?)null;
        var acOnline = powerStatus.PowerLineStatus == PowerLineStatus.Online;
        var batteryLow = hasBattery && batteryPercent <= config.LowBatteryThresholdPercent && !acOnline;

        if (!acOnline)
            issues.Add(new SystemHealthIssue("ac_power_offline", GuardianSeverity.Warning, "Power: Battery", "Bilgisayar prize bağlı değil."));

        if (batteryLow)
            issues.Add(new SystemHealthIssue("battery_low", GuardianSeverity.Critical, "Battery Low", $"Batarya %{batteryPercent}."));

        var pendingReboot = IsPendingReboot();
        if (pendingReboot)
            issues.Add(new SystemHealthIssue("pending_reboot", GuardianSeverity.Warning, "Pending Reboot", "Windows yeniden başlatma bekliyor."));

        var network = await CheckNetworkAsync(config.NetworkPingTarget, cancellationToken).ConfigureAwait(false);
        if (!network.Available)
            issues.Add(new SystemHealthIssue("network_failed", GuardianSeverity.Warning, "Network Failed", network.Message));

        var disk = GetSystemDiskInfo(config.CriticalDiskFreePercent);
        if (disk.Critical)
            issues.Add(new SystemHealthIssue("system_disk_critical", GuardianSeverity.Critical, "System Disk Critical", $"{disk.Name} boş alan %{disk.FreePercent:F1}."));

        PowerSettingsSnapshot? settings = null;
        try
        {
            settings = await powerSettingsService.GetCurrentSettingsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logService.Error("Power settings snapshot failed.", ex);
            issues.Add(new SystemHealthIssue("power_settings_unavailable", GuardianSeverity.Warning, "Power Settings", "Aktif güç planı okunamadı."));
        }

        if (heartbeat.LastHeartbeatSucceeded == false)
            issues.Add(new SystemHealthIssue("heartbeat_failed", GuardianSeverity.Warning, "Heartbeat Failed", heartbeat.Message));

        return new SystemHealthSnapshot(
            DateTime.UtcNow,
            guardActive,
            acOnline,
            hasBattery,
            batteryPercent,
            batteryLow,
            pendingReboot,
            network.Available,
            network.LatencyMs,
            network.Message,
            disk.Critical,
            disk.Name,
            disk.FreePercent,
            disk.FreeGb,
            settings,
            heartbeat,
            issues);
    }

    private static bool IsPendingReboot()
    {
        try
        {
            if (Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending") is not null)
                return true;

            if (Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") is not null)
                return true;

            using var sessionManager = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
            return sessionManager?.GetValue("PendingFileRenameOperations") is not null;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(bool Available, long? LatencyMs, string Message)> CheckNetworkAsync(
        string target,
        CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(target, 3000).WaitAsync(cancellationToken).ConfigureAwait(false);
            return reply.Status == IPStatus.Success
                ? (true, reply.RoundtripTime, $"OK ({reply.RoundtripTime} ms)")
                : (false, null, $"Ping failed: {reply.Status}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, null, "Ağ bağlantısı kontrol edilemedi.");
        }
    }

    private static (string Name, double FreePercent, double FreeGb, bool Critical) GetSystemDiskInfo(int criticalFreePercent)
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(root);
            if (!drive.IsReady)
                return (root, 0, 0, true);

            var freePercent = drive.TotalSize > 0 ? drive.TotalFreeSpace * 100.0 / drive.TotalSize : 0;
            var freeGb = drive.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0);
            return (drive.Name, freePercent, freeGb, freePercent <= criticalFreePercent);
        }
        catch
        {
            return ("System", 0, 0, true);
        }
    }
}

internal sealed class NotificationService : INotificationService, IDisposable
{
    private readonly IGuardianConfigService _configService;
    private readonly IGuardianLogService _logService;
    private readonly Dictionary<string, DateTime> _lastAlertUtcByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly NotifyIcon _notifyIcon;

    public NotificationService(IGuardianConfigService configService, IGuardianLogService logService)
    {
        _configService = configService;
        _logService = logService;
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "NoSleep Guardian",
            Visible = true,
        };
    }

    public HeartbeatState Heartbeat { get; private set; } = new(null, null, "Not sent yet");

    public Task<NotificationResult> SendTestAsync(CancellationToken cancellationToken = default) =>
        SendAsync(
            "NoSleep Guardian test",
            "Test bildirimi başarıyla tetiklendi.",
            GuardianSeverity.Info,
            includeLocalNotification: true,
            cancellationToken);

    public async Task<NotificationResult> SendAlertAsync(
        string alertKey,
        string title,
        string message,
        GuardianSeverity severity,
        CancellationToken cancellationToken = default)
    {
        var cooldown = TimeSpan.FromMinutes(_configService.Current.AlertCooldownMinutes);
        if (_lastAlertUtcByKey.TryGetValue(alertKey, out var lastSent) && DateTime.UtcNow - lastSent < cooldown)
            return new NotificationResult(false, HasRemoteNotificationConfig(), false, true, "Alert cooldown aktif.");

        _lastAlertUtcByKey[alertKey] = DateTime.UtcNow;
        return await SendAsync(title, message, severity, includeLocalNotification: true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<NotificationResult> SendHeartbeatAsync(SystemHealthSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var message =
            $"Guard: {(snapshot.GuardActive ? "ON" : "OFF")} | " +
            $"Power: {(snapshot.AcPowerOnline ? "Online" : "Battery")} | " +
            $"Network: {(snapshot.NetworkAvailable ? "OK" : "Failed")} | " +
            $"Disk: {snapshot.SystemDiskFreePercent:F1}% free";

        var result = await SendAsync("NoSleep Guardian heartbeat", message, GuardianSeverity.Info, includeLocalNotification: false, cancellationToken)
            .ConfigureAwait(false);
        Heartbeat = new HeartbeatState(
            DateTime.UtcNow,
            result.RemoteConfigured ? result.RemoteDelivered : null,
            result.Message);
        return result;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private async Task<NotificationResult> SendAsync(
        string title,
        string message,
        GuardianSeverity severity,
        bool includeLocalNotification,
        CancellationToken cancellationToken)
    {
        var localDelivered = false;
        if (includeLocalNotification && _configService.Current.EnableLocalNotifications)
        {
            try
            {
                _notifyIcon.BalloonTipTitle = title;
                _notifyIcon.BalloonTipText = message;
                _notifyIcon.BalloonTipIcon = severity switch
                {
                    GuardianSeverity.Critical => ToolTipIcon.Error,
                    GuardianSeverity.Warning => ToolTipIcon.Warning,
                    _ => ToolTipIcon.Info,
                };
                _notifyIcon.ShowBalloonTip(5000);
                localDelivered = true;
            }
            catch (Exception ex)
            {
                _logService.Error("Local notification failed.", ex);
            }
        }

        var remoteConfigured = HasRemoteNotificationConfig();
        if (!remoteConfigured)
            return new NotificationResult(localDelivered, false, false, false, BuildNotificationMessage(localDelivered, false, "Remote notification not configured."));

        var remoteDelivered = await SendRemoteAsync(title, message, severity, cancellationToken).ConfigureAwait(false);
        return new NotificationResult(localDelivered, true, remoteDelivered, false, BuildNotificationMessage(localDelivered, remoteDelivered, remoteDelivered ? null : "Remote notification failed."));
    }

    private async Task<bool> SendRemoteAsync(string title, string message, GuardianSeverity severity, CancellationToken cancellationToken)
    {
        var config = _configService.Current;
        var payloadText = $"[{severity}] {title}\n{message}";
        var anySuccess = false;

        if (IsTelegramConfigured(config))
            anySuccess |= await TrySendTelegramAsync(config.Telegram, payloadText, cancellationToken).ConfigureAwait(false);

        if (IsDiscordConfigured(config))
            anySuccess |= await TryPostJsonAsync(config.Discord.WebhookUrl, new { content = payloadText }, cancellationToken).ConfigureAwait(false);

        if (IsGenericWebhookConfigured(config))
        {
            anySuccess |= await TryPostJsonAsync(
                config.GenericWebhook.Url,
                new
                {
                    source = "Assist NoSleep Guardian",
                    title,
                    message,
                    severity = severity.ToString(),
                    timestampUtc = DateTime.UtcNow,
                },
                cancellationToken).ConfigureAwait(false);
        }

        return anySuccess;
    }

    private async Task<bool> TrySendTelegramAsync(TelegramNotificationConfig config, string message, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://api.telegram.org/bot{Uri.EscapeDataString(config.BotToken)}/sendMessage";
            var payload = new { chat_id = config.ChatId, text = message };
            return await TryPostJsonAsync(url, payload, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logService.Warning($"Telegram notification failed: {ex.GetType().Name}.");
            return false;
        }
    }

    private async Task<bool> TryPostJsonAsync(string url, object payload, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await AppConstants.SharedHttpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return true;

            _logService.Warning($"Remote notification endpoint returned HTTP {(int)response.StatusCode}.");
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logService.Warning($"Remote notification request failed: {ex.GetType().Name}.");
            return false;
        }
    }

    private bool HasRemoteNotificationConfig()
    {
        var config = _configService.Current;
        return IsTelegramConfigured(config) || IsDiscordConfigured(config) || IsGenericWebhookConfigured(config);
    }

    private static bool IsTelegramConfigured(GuardianConfig config) =>
        config.Telegram.Enabled &&
        !string.IsNullOrWhiteSpace(config.Telegram.BotToken) &&
        !string.IsNullOrWhiteSpace(config.Telegram.ChatId);

    private static bool IsDiscordConfigured(GuardianConfig config) =>
        config.Discord.Enabled && !string.IsNullOrWhiteSpace(config.Discord.WebhookUrl);

    private static bool IsGenericWebhookConfigured(GuardianConfig config) =>
        config.GenericWebhook.Enabled && !string.IsNullOrWhiteSpace(config.GenericWebhook.Url);

    private static string BuildNotificationMessage(bool localDelivered, bool remoteDelivered, string? note)
    {
        var parts = new List<string>();
        if (localDelivered) parts.Add("Local notification sent.");
        if (remoteDelivered) parts.Add("Remote notification sent.");
        if (!string.IsNullOrWhiteSpace(note)) parts.Add(note);
        return string.Join(" ", parts);
    }
}

internal sealed class GuardianConfigService(IGuardianLogService logService) : IGuardianConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string ConfigFilePath { get; } = Path.Combine(AppConstants.AppDataPath, "nosleep-guardian.json");
    public GuardianConfig Current { get; private set; } = new();

    public GuardianConfig Load()
    {
        try
        {
            Directory.CreateDirectory(AppConstants.AppDataPath);
            if (!File.Exists(ConfigFilePath))
            {
                Current = new GuardianConfig();
                Save();
                return Current;
            }

            var json = File.ReadAllText(ConfigFilePath);
            Current = JsonSerializer.Deserialize<GuardianConfig>(json) ?? new GuardianConfig();
            Normalize(Current);
        }
        catch (Exception ex)
        {
            logService.Error("NoSleep Guardian config could not be loaded.", ex);
            Current = new GuardianConfig();
        }

        return Current;
    }

    public void Save()
    {
        Normalize(Current);
        Directory.CreateDirectory(AppConstants.AppDataPath);
        File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(Current, JsonOptions));
    }

    public string GetMaskedRemoteSummary()
    {
        var config = Current;
        var telegram = config.Telegram.Enabled && !string.IsNullOrWhiteSpace(config.Telegram.BotToken)
            ? $"Telegram: enabled token={Mask(config.Telegram.BotToken)} chat={Mask(config.Telegram.ChatId)}"
            : "Telegram: not configured";
        var discord = config.Discord.Enabled && !string.IsNullOrWhiteSpace(config.Discord.WebhookUrl)
            ? $"Discord: enabled url={Mask(config.Discord.WebhookUrl)}"
            : "Discord: not configured";
        var generic = config.GenericWebhook.Enabled && !string.IsNullOrWhiteSpace(config.GenericWebhook.Url)
            ? $"Webhook: enabled url={Mask(config.GenericWebhook.Url)}"
            : "Webhook: not configured";

        return $"{telegram}; {discord}; {generic}";
    }

    public string MaskSecrets(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var sanitized = value;
        var config = Current;
        foreach (var secret in new[]
        {
            config.Telegram.BotToken,
            config.Telegram.ChatId,
            config.Discord.WebhookUrl,
            config.GenericWebhook.Url,
        })
        {
            if (!string.IsNullOrWhiteSpace(secret))
                sanitized = sanitized.Replace(secret, Mask(secret), StringComparison.Ordinal);
        }

        return sanitized;
    }

    private static void Normalize(GuardianConfig config)
    {
        config.HeartbeatIntervalMinutes = Math.Clamp(config.HeartbeatIntervalMinutes, 1, 24 * 60);
        config.AlertCooldownMinutes = Math.Clamp(config.AlertCooldownMinutes, 1, 24 * 60);
        config.LowBatteryThresholdPercent = Math.Clamp(config.LowBatteryThresholdPercent, 1, 90);
        config.CriticalDiskFreePercent = Math.Clamp(config.CriticalDiskFreePercent, 1, 50);
        if (string.IsNullOrWhiteSpace(config.NetworkPingTarget))
            config.NetworkPingTarget = "8.8.8.8";
        config.Telegram ??= new TelegramNotificationConfig();
        config.Discord ??= new DiscordNotificationConfig();
        config.GenericWebhook ??= new GenericWebhookConfig();
    }

    private static string Mask(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "(empty)";

        return value.Length <= 8
            ? new string('*', value.Length)
            : $"{value[..4]}...{value[^4..]}";
    }
}

internal sealed class GuardianLogService : IGuardianLogService
{
    private readonly object _sync = new();

    public string LogFilePath { get; } = Path.Combine(AppConstants.AppDataPath, "nosleep-guardian.log");

    public void Info(string message) => Write("INFO", message, null);
    public void Warning(string message) => Write("WARN", message, null);
    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            Directory.CreateDirectory(AppConstants.AppDataPath);
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} [{level}] {message}";
            if (exception is not null)
                line += $" {exception.GetType().Name}: {exception.Message}";

            lock (_sync)
                File.AppendAllText(LogFilePath, line + Environment.NewLine);
        }
        catch
        {
            // Logging must never break the guardian flow.
        }
    }
}
