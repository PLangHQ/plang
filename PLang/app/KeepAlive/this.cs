namespace app.KeepAlive;

/// <summary>
/// App-level "keep alive" collection. Disposable objects added here live for
/// the life of the App and get disposed on App.DisposeAsync. One per app —
/// owns its own list, its own Add / Remove discipline, and its own teardown.
/// </summary>
public sealed class @this : IAsyncDisposable
{
    private readonly List<object> _items = new();
    private bool _disposed;

    /// <summary>Promotes an object to app-level lifetime. Disposed on DisposeAsync.</summary>
    public void Add(object instance) => _items.Add(instance);

    /// <summary>
    /// Removes the object from the collection AND disposes it synchronously.
    /// Sync dispose mirrors the prior App.RemoveKeepAlive semantics — callers
    /// reach this from non-async paths.
    /// </summary>
    public void Remove(object instance)
    {
        if (!_items.Remove(instance)) return;
        if (instance is IAsyncDisposable ad) ad.DisposeAsync().AsTask().GetAwaiter().GetResult();
        else if (instance is IDisposable d) d.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var item in _items)
        {
            if (item is IAsyncDisposable ad) await ad.DisposeAsync();
            else if (item is IDisposable d) d.Dispose();
        }
        _items.Clear();
    }
}
