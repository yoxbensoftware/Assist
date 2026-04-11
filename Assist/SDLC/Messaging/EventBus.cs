using System.Collections.Concurrent;

namespace Assist.SDLC.Messaging;

using Assist.SDLC.Abstractions;
using Assist.SDLC.Domain;

/// <summary>
/// In-process publish / subscribe event bus.
/// Thread-safe; handlers run on the caller's synchronization context when available.
/// </summary>
internal sealed class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<SdlcEventType, ConcurrentBag<Action<SdlcEvent>>> _typed = new();
    private readonly ConcurrentBag<Action<SdlcEvent>> _global = [];
    private readonly ConcurrentBag<SdlcEvent> _history = [];

    public void Publish(SdlcEvent evt)
    {
        _history.Add(evt);

        if (_typed.TryGetValue(evt.Type, out var handlers))
        {
            foreach (var h in handlers)
                SafeInvoke(h, evt);
        }

        foreach (var h in _global)
            SafeInvoke(h, evt);
    }

    public IDisposable Subscribe(SdlcEventType type, Action<SdlcEvent> handler)
    {
        var bag = _typed.GetOrAdd(type, _ => []);
        bag.Add(handler);
        return new Unsubscriber(() => Remove(bag, handler));
    }

    public IDisposable SubscribeAll(Action<SdlcEvent> handler)
    {
        _global.Add(handler);
        return new Unsubscriber(() => Remove(_global, handler));
    }

    public IReadOnlyList<SdlcEvent> GetHistory() => [.. _history];

    // ── helpers ───────────────────────────────────────────

    private static void SafeInvoke(Action<SdlcEvent> handler, SdlcEvent evt)
    {
        try { handler(evt); }
        catch { /* swallow — structured logging TODO */ }
    }

    private static void Remove(ConcurrentBag<Action<SdlcEvent>> bag, Action<SdlcEvent> handler)
    {
        // ConcurrentBag doesn't support removal; rebuild without the handler.
        var items = bag.Where(h => h != handler).ToList();
        while (bag.TryTake(out _)) { }
        foreach (var item in items)
            bag.Add(item);
    }

    private sealed class Unsubscriber(Action onDispose) : IDisposable
    {
        private Action? _onDispose = onDispose;
        public void Dispose() => Interlocked.Exchange(ref _onDispose, null)?.Invoke();
    }
}
