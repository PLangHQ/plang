namespace App.Errors;

/// <summary>
/// Errors namespace root: AsyncLocal-flowed current error scope + run-wide audit.
/// Replaces the old <c>Actor.Context.Error</c> property and the parallel
/// <c>vars.Set("!error", ...)</c> registration.
///
/// <c>%!error%</c> in PLang resolves through <see cref="Error"/>: an error.handle.Wrap
/// pushes the caught error before invoking its recovery body and disposes the scope after,
/// so nested handlers see their own error and parallel branches don't pollute each other
/// (AsyncLocal forks naturally on Task.WhenAll).
/// </summary>
public sealed partial class @this
{
    // Instance-level (not static) AsyncLocal — multiple App instances in the same test
    // process must not see each other's Current. Mirrors CallStack/this.cs which moved
    // _current to instance scope for the same reason; before this fix, `All` was already
    // per-instance but `Error` read process-wide static.
    private readonly AsyncLocal<IError?> _current = new();

    /// <summary>
    /// The current error in this async context. Null outside any <see cref="Push"/> scope.
    /// PLang reads this through <c>%!error%</c>.
    /// </summary>
    public IError? Error => _current.Value;

    /// <summary>
    /// Run-wide trail of every error pushed into scope. Survives Pop.
    /// See <see cref="Trail.@this"/> for thread-safety + lifecycle.
    /// </summary>
    public Trail.@this Trail { get; } = new();

    /// <summary>
    /// Pushes <paramref name="error"/> as the current error and appends to <see cref="Trail"/>.
    /// The returned disposable restores the previous AsyncLocal value on Dispose — use
    /// <c>using</c> or <c>using var</c> at the call site.
    /// </summary>
    public IDisposable Push(IError error)
    {
        var previous = _current.Value;
        _current.Value = error;
        Trail.Add(error);
        return new Restorer(_current, previous);
    }

    private sealed class Restorer : IDisposable
    {
        private readonly AsyncLocal<IError?> _slot;
        private readonly IError? _previous;
        private bool _disposed;

        public Restorer(AsyncLocal<IError?> slot, IError? previous)
        {
            _slot = slot;
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _slot.Value = _previous;
        }
    }
}
