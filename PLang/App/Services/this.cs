using System.Collections;
using System.Collections.Concurrent;

namespace App.Services;

/// <summary>
/// Flat per-call Service collection on App. Each Service represents one outbound
/// call's I/O scope (Channels, Identity, Parent reference). Replaces the
/// runtime1 Service-as-actor model: identity is always System; the Parent ref
/// preserves "who triggered this" without making Service an Actor.
/// </summary>
public sealed class @this : IEnumerable<global::App.Services.Service.@this>
{
    private readonly ConcurrentBag<global::App.Services.Service.@this> _services = new();
    private readonly global::App.@this _app;

    public @this(global::App.@this app) { _app = app; }

    /// <summary>
    /// Spawns a new Service whose Parent is <paramref name="parent"/>. Caller
    /// disposes when the call completes (await using).
    /// </summary>
    public global::App.Services.Service.@this New(global::App.Actor.@this parent)
    {
        var service = new global::App.Services.Service.@this(this, parent);
        _services.Add(service);
        return service;
    }

    internal void Remove(global::App.Services.Service.@this service)
    {
        // ConcurrentBag has no Remove; rebuild without the disposed service.
        var keep = _services.Where(s => !ReferenceEquals(s, service)).ToList();
        while (_services.TryTake(out _)) { }
        foreach (var s in keep) _services.Add(s);
    }

    public int Count => _services.Count;

    public IEnumerator<global::App.Services.Service.@this> GetEnumerator() => _services.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
