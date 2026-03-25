namespace Assist.Services;

/// <summary>
/// Thread-safe clipboard history service that stores last N text entries.
/// Uses polling to detect clipboard changes and filters sensitive content.
/// </summary>
internal sealed class ClipboardHistoryService : IDisposable
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

    /// <summary>
    /// Initializes a new clipboard history service with the specified capacity and filtering options.
    /// </summary>
    public ClipboardHistoryService(int capacity = 50, bool filterSensitive = true)
    {
        _capacity = capacity;
        _filterSensitive = filterSensitive;
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
        Instance = this;
    }

    /// <summary>
    /// Starts polling the clipboard for changes at the specified interval in milliseconds.
    /// </summary>
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

    /// <summary>
    /// Stops the clipboard polling timer.
    /// </summary>
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

    /// <summary>
    /// Updates the capacity, polling interval, and sensitivity filter settings, then restarts polling.
    /// </summary>
    public void SetOptions(int capacity, int intervalMs, bool filterSensitive)
    {
        _capacity = capacity;
        _filterSensitive = filterSensitive;
        Start(intervalMs);
    }

    /// <summary>
    /// Returns the current capacity, polling interval, and sensitivity filter settings.
    /// </summary>
    public (int capacity, int intervalMs, bool filterSensitive) GetOptions()
        => (_capacity, _intervalMs, _filterSensitive);

    /// <summary>
    /// Adds a text entry to the history, skipping consecutive duplicates and trimming excess entries.
    /// </summary>
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

    /// <summary>
    /// Returns a snapshot of all clipboard history entries.
    /// </summary>
    public List<string> GetAll()
    {
        lock (_lock)
        {
            return [.. _items];
        }
    }

    /// <summary>
    /// Clears all clipboard history entries.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
        }
    }

    /// <summary>
    /// Disposes the polling timer and clears the singleton instance.
    /// </summary>
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

    /// <summary>
    /// Polls the clipboard for new text content and adds it to history if applicable.
    /// </summary>
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

    /// <summary>
    /// Reads clipboard text on the UI thread and returns it asynchronously.
    /// </summary>
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

    /// <summary>
    /// Checks whether the given text was recently set by the application itself.
    /// </summary>
    private bool IsRecentAppSet(string text)
    {
        if (string.IsNullOrEmpty(_lastAppSet) || text != _lastAppSet)
            return false;

        var age = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - _lastAppSetTicks);
        return age < AppSetCooldown;
    }

    /// <summary>
    /// Determines whether the text looks like a sensitive value such as a password or token.
    /// </summary>
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
