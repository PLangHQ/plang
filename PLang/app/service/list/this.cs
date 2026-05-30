using System.Collections;
using System.Collections.Concurrent;

namespace app.service.list;

/// <summary>
/// Flat per-call Service collection on App. Each Service represents one outbound
/// call's I/O scope (Channels, Identity, Parent reference). Replaces the
/// runtime1 Service-as-actor model: identity is always System; the Parent ref
/// preserves "who triggered this" without making Service an Actor.
/// </summary>
public sealed class @this : IEnumerable<global::app.service.@this>
{
    private readonly ConcurrentDictionary<Guid, global::app.service.@this> _services = new();
    private readonly global::app.@this _app;

    public @this(global::app.@this app) { _app = app; }

    /// <summary>
    /// Spawns a new Service whose Parent is <paramref name="parent"/>. Caller
    /// disposes when the call completes (await using).
    /// </summary>
    public global::app.service.@this New(global::app.actor.@this parent)
    {
        var service = new global::app.service.@this(this, parent);
        _services.TryAdd(service.Id, service);
        return service;
    }

    internal void Remove(global::app.service.@this service)
    {
        // Atomic — Service.Id is the stable key. Concurrent New() during Remove
        // can't be lost the way the prior ConcurrentBag drain-and-rebuild allowed.
        _services.TryRemove(service.Id, out _);
    }

    public int Count => _services.Count;

    public IEnumerator<global::app.service.@this> GetEnumerator() => _services.Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
