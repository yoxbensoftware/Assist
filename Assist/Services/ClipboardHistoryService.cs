namespace Assist.Services;

/// <summary>
/// Thread-safe clipboard history service that stores last N text entries.
/// Uses polling to detect clipboard changes and filters sensitive content.
/// </summary>
public sealed class ClipboardHistoryService : IDisposable
{
    private const int SensitiveMinLength = 6;
    private const int SensitiveMaxLength = 128;
    private static readonly TimeSpan AppSetCooldown = TimeSpan.FromSeconds(3);

    public static ClipboardHistoryService? Instance { get; private set; }

    private readonly LinkedList<string> _items = new();
    private readonly SynchronizationContext _uiContext;
    private readonly object _lock = new();

    private System.Threading.Timer? _pollTimer;
    private int _capacity;
    private int _intervalMs = 1000;
    private bool _filterSensitive;
    private string? _lastSeen;
    private string? _lastAppSet;
    private long _lastAppSetTicks;
    private bool _disposed;

    public ClipboardHistoryService(int capacity = 50, bool filterSensitive = true)
    {
        _capacity = capacity;
        _filterSensitive = filterSensitive;
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
        Instance = this;
    }

    public void Start(int intervalMs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Stop();
        _intervalMs = intervalMs;
        _pollTimer = new System.Threading.Timer(
            async _ => await PollClipboardAsync().ConfigureAwait(false),
            null,
            intervalMs,
            intervalMs);
    }

    public void Stop()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    /// <summary>
    /// Notifies the service that the application set the clipboard programmatically.
    /// This prevents the same text from being added to history.
    /// </summary>
    public void NotifyClipboardSetByApp(string text)
    {
        _lastAppSet = text;
        _lastAppSetTicks = DateTime.UtcNow.Ticks;
    }

    public void SetOptions(int capacity, int intervalMs, bool filterSensitive)
    {
        _capacity = capacity;
        _filterSensitive = filterSensitive;
        Start(intervalMs);
    }

    public (int capacity, int intervalMs, bool filterSensitive) GetOptions()
        => (_capacity, _intervalMs, _filterSensitive);

    public void Add(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        lock (_lock)
        {
            // Avoid consecutive duplicates
            if (_items.First?.Value == text) return;

            _items.AddFirst(text);
            while (_items.Count > _capacity)
            {
                _items.RemoveLast();
            }
        }
    }

    public List<string> GetAll()
    {
        lock (_lock)
        {
            return [.. _items];
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _pollTimer?.Dispose();
        _pollTimer = null;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private async Task PollClipboardAsync()
    {
        try
        {
            var text = await GetClipboardTextAsync().ConfigureAwait(false);

            if (string.IsNullOrEmpty(text) || text == _lastSeen)
                return;

            // Skip if app just set this text recently
            if (IsRecentAppSet(text))
            {
                _lastSeen = text;
                return;
            }

            _lastSeen = text;

            // Skip sensitive content if filtering is enabled
            if (_filterSensitive && IsSensitive(text))
                return;

            Add(text);
        }
        catch
        {
            // Ignore polling errors
        }
    }

    private Task<string?> GetClipboardTextAsync()
    {
        var tcs = new TaskCompletionSource<string?>();
        _uiContext.Post(_ =>
        {
            try
            {
                tcs.SetResult(Clipboard.ContainsText() ? Clipboard.GetText() : null);
            }
            catch
            {
                tcs.SetResult(null);
            }
        }, null);
        return tcs.Task;
    }

    private bool IsRecentAppSet(string text)
    {
        if (string.IsNullOrEmpty(_lastAppSet) || text != _lastAppSet)
            return false;

        var age = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - _lastAppSetTicks);
        return age < AppSetCooldown;
    }

    private static bool IsSensitive(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Contains(' '))
            return false;

        var len = text.Length;
        if (len < SensitiveMinLength || len > SensitiveMaxLength)
            return false;

        bool hasLetter = false, hasDigit = false, hasSpecial = false;
        foreach (var c in text)
        {
            if (char.IsLetter(c)) hasLetter = true;
            else if (char.IsDigit(c)) hasDigit = true;
            else hasSpecial = true;
        }

        // Treat as sensitive if contains mixed alphanumeric or special chars
        return (hasLetter && hasDigit) || hasSpecial;
    }
}
