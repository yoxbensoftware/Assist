namespace Assist.Services;

/// <summary>
/// Thread-safe clipboard history service that stores last N text entries.
/// Uses polling to detect clipboard changes and filters sensitive content.
/// </summary>
internal sealed class ClipboardHistoryService : IDisposable
{
    private const int SensitiveMinLength = 6;
    private const int SensitiveMaxLength = 128;
    private const int MaxStoredTextLength = 16 * 1024;
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
    private long _version;
    private int _pollInProgress;
    private bool _disposed;

    /// <summary>
    /// Initializes a new clipboard history service with the specified capacity and filtering options.
    /// </summary>
    public ClipboardHistoryService(int capacity = 50, bool filterSensitive = true)
    {
        _capacity = Math.Clamp(capacity, 1, 1000);
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
        _intervalMs = Math.Clamp(intervalMs, 500, 60_000);
        _pollTimer = new System.Threading.Timer(
            static state => _ = ((ClipboardHistoryService)state!).PollClipboardGuardedAsync(),
            this,
            _intervalMs,
            _intervalMs);
    }

    private async Task PollClipboardGuardedAsync()
    {
        if (_disposed || Interlocked.Exchange(ref _pollInProgress, 1) == 1)
            return;

        try
        {
            await PollClipboardAsync().ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _pollInProgress, 0);
        }
    }

    /// <summary>
    /// Monotonically increasing version used by UI refreshers to skip unchanged snapshots.
    /// </summary>
    public long Version => Volatile.Read(ref _version);

    /// <summary>
    /// Maximum text length stored for a single clipboard entry.
    /// </summary>
    public static int MaxEntryLength => MaxStoredTextLength;

    /// <summary>
    /// Stops the clipboard polling timer.
    /// </summary>
    public void Stop()
    {
        var timer = _pollTimer;
        _pollTimer = null;
        timer?.Dispose();
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
        lock (_lock)
        {
            _capacity = Math.Clamp(capacity, 1, 1000);
            TrimToCapacity();
            Interlocked.Increment(ref _version);
        }
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
        text = TrimStoredText(text);

        lock (_lock)
        {
            // Avoid consecutive duplicates
            if (_items.First?.Value == text) return;

            _items.AddFirst(text);
            TrimToCapacity();
            Interlocked.Increment(ref _version);
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
            Interlocked.Increment(ref _version);
        }
    }

    /// <summary>
    /// Disposes the polling timer and clears the singleton instance.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();

        Clear();

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
        if (_disposed) return;

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
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _uiContext.Post(_ =>
        {
            try
            {
                if (_disposed)
                {
                    tcs.TrySetResult(null);
                    return;
                }

                tcs.TrySetResult(Clipboard.ContainsText() ? Clipboard.GetText() : null);
            }
            catch
            {
                tcs.TrySetResult(null);
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

    private void TrimToCapacity()
    {
        while (_items.Count > _capacity)
        {
            _items.RemoveLast();
        }
    }

    private static string TrimStoredText(string text)
    {
        if (text.Length <= MaxStoredTextLength)
            return text;

        return text[..MaxStoredTextLength] + Environment.NewLine + "[clipboard text truncated]";
    }
}
