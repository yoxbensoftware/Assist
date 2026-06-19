namespace Assist.Models;

internal enum GuardianSeverity
{
    Info,
    Warning,
    Critical,
}

internal sealed class GuardianConfig
{
    public int HeartbeatIntervalMinutes { get; set; } = 15;
    public int AlertCooldownMinutes { get; set; } = 10;
    public int LowBatteryThresholdPercent { get; set; } = 20;
    public int CriticalDiskFreePercent { get; set; } = 10;
    public string NetworkPingTarget { get; set; } = "8.8.8.8";
    public bool EnableLocalNotifications { get; set; } = true;
    public TelegramNotificationConfig Telegram { get; set; } = new();
    public DiscordNotificationConfig Discord { get; set; } = new();
    public GenericWebhookConfig GenericWebhook { get; set; } = new();
}

internal sealed class TelegramNotificationConfig
{
    public bool Enabled { get; set; }
    public string BotToken { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
}

internal sealed class DiscordNotificationConfig
{
    public bool Enabled { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;
}

internal sealed class GenericWebhookConfig
{
    public bool Enabled { get; set; }
    public string Url { get; set; } = string.Empty;
}

internal sealed record PowerSettingsSnapshot(
    Guid ActiveSchemeId,
    string ActiveSchemeName,
    uint SleepAcSeconds,
    uint SleepDcSeconds,
    uint HibernateAcSeconds,
    uint HibernateDcSeconds,
    bool BackupAvailable);

internal sealed record PowerSettingsBackup(
    Guid ActiveSchemeId,
    DateTime CreatedAtUtc,
    uint SleepAcSeconds,
    uint SleepDcSeconds,
    uint HibernateAcSeconds,
    uint HibernateDcSeconds);

internal sealed record PowerSettingsOperationResult(bool Success, string Message);

internal sealed record HeartbeatState(DateTime? LastHeartbeatUtc, bool? LastHeartbeatSucceeded, string Message);

internal sealed record SystemHealthIssue(string Key, GuardianSeverity Severity, string Title, string Message);

internal sealed record SystemHealthSnapshot(
    DateTime CheckedAtUtc,
    bool GuardActive,
    bool AcPowerOnline,
    bool HasBattery,
    int? BatteryPercent,
    bool BatteryLow,
    bool PendingReboot,
    bool NetworkAvailable,
    long? NetworkLatencyMs,
    string NetworkMessage,
    bool SystemDiskCritical,
    string SystemDiskName,
    double SystemDiskFreePercent,
    double SystemDiskFreeGb,
    PowerSettingsSnapshot? PowerSettings,
    HeartbeatState Heartbeat,
    IReadOnlyList<SystemHealthIssue> Issues);

internal sealed record NotificationResult(
    bool LocalDelivered,
    bool RemoteConfigured,
    bool RemoteDelivered,
    bool SkippedByCooldown,
    string Message)
{
    public bool IsSuccessful => LocalDelivered || RemoteDelivered || SkippedByCooldown;
}
