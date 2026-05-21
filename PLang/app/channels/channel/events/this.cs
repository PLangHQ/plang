using EventBinding = global::app.events.lifecycle.bindings.binding.@this;
using EventType = global::app.events.EventType;

namespace app.channels.channel.events;

/// <summary>
/// Per-channel event bindings + recursion guard. Encapsulates the binding list,
/// its lock discipline, the AsyncLocal "this binding is already firing" guard,
/// and the per-channel filter — none of which leaked to <c>Channel.@this</c>
/// before. Same shape spirit as <c>app.goals.goal.Events</c>: the type that owns
/// the data also owns its access rules.
///
/// The cross-source firing orchestration (per-channel → per-actor → app-level)
/// stays on Channel because it spans Channel + Actor + App; this type only
/// handles the per-channel slice plus the guard, since both belong to one
/// channel.
/// </summary>
public sealed class @this
{
    private readonly List<EventBinding> _list = new();
    private readonly object _lock = new();
    private readonly AsyncLocal<HashSet<string>?> _active = new();

    public int Count
    {
        get { lock (_lock) return _list.Count; }
    }

    public void Add(EventBinding binding)
    {
        if (binding == null) return;
        lock (_lock) _list.Add(binding);
    }

    /// <summary>
    /// Per-channel bindings that match the given event <paramref name="type"/> and
    /// the channel's <paramref name="channelName"/> (a binding with a null
    /// ChannelName matches any channel; a non-null one must equal-ignore-case).
    /// Returned as a snapshot so the caller can iterate without holding the lock
    /// while invoking handlers.
    /// </summary>
    public IEnumerable<EventBinding> Match(EventType type, string channelName)
    {
        EventBinding[] snapshot;
        lock (_lock) snapshot = _list.ToArray();
        foreach (var b in snapshot)
        {
            if (b.Type != type) continue;
            if (b.ChannelName == null
                || string.Equals(b.ChannelName, channelName, StringComparison.OrdinalIgnoreCase))
                yield return b;
        }
    }

    /// <summary>
    /// True if <paramref name="bindingId"/> is already firing on the current
    /// async flow (recursion check). Use with <see cref="Enter"/> to guard a
    /// before-handler that writes to the same channel from re-triggering itself.
    /// </summary>
    public bool IsActive(string bindingId)
        => _active.Value?.Contains(bindingId) ?? false;

    /// <summary>
    /// Marks <paramref name="bindingId"/> active on the current async flow until
    /// the returned scope disposes. Pair with <see cref="IsActive"/> to skip
    /// re-entry. AsyncLocal ensures concurrent fires on different flows don't
    /// see each other's active set.
    /// </summary>
    public IDisposable Enter(string bindingId)
    {
        var parent = _active.Value;
        var set = parent == null
            ? new HashSet<string> { bindingId }
            : new HashSet<string>(parent) { bindingId };
        _active.Value = set;
        return new Releaser(this, parent);
    }

    private sealed class Releaser : IDisposable
    {
        private readonly @this _owner;
        private readonly HashSet<string>? _parent;
        public Releaser(@this owner, HashSet<string>? parent) { _owner = owner; _parent = parent; }
        public void Dispose() => _owner._active.Value = _parent;
    }
}
