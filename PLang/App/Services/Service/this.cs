using App.modules.identity;

namespace App.Services.Service;

/// <summary>
/// Per-outbound-call I/O scope. Not an Actor — Service holds Channels (per-call
/// channel set), Identity (always System's), and a Parent reference to the Actor
/// that triggered the call.
///
/// Lifetime is bounded by the call (HTTP request/response) or the connection
/// (TCP, WebSocket). Spawned via <see cref="@this.New"/> on <see cref="App.Services.@this"/>;
/// disposed removes from the collection and tears down the channels.
/// </summary>
public sealed class @this : IAsyncDisposable
{
    private readonly App.Services.@this _collection;

    /// <summary>The actor that spawned this Service (audit / tracing).</summary>
    public global::App.Actor.@this Parent { get; }

    /// <summary>This service's per-call channel set.</summary>
    public global::App.Channels.@this Channels { get; }

    /// <summary>
    /// Always the System actor's identity. Outbound calls go under the app's
    /// name regardless of which Actor triggered them.
    /// </summary>
    public Identity? Identity => Parent.App.System.Identity;

    internal @this(App.Services.@this collection, global::App.Actor.@this parent)
    {
        _collection = collection;
        Parent = parent;
        Channels = new global::App.Channels.@this(parent.App);
    }

    public async ValueTask DisposeAsync()
    {
        _collection.Remove(this);
        await Channels.DisposeAsync();
    }
}
