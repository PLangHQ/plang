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
    private static readonly AsyncLocal<IError?> _current = new();

    /// <summary>
    /// The current error in this async context. Null outside any <see cref="Push"/> scope.
    /// PLang reads this through <c>%!error%</c>.
    /// </summary>
    public IError? Error => _current.Value;

    /// <summary>
    /// Run-wide append-only audit of every error pushed into scope. Survives Pop.
    /// Use this for "did anything fail during this run, even if recovered?"
    /// </summary>
    public List<IError> All { get; } = new();

    /// <summary>
    /// Pushes <paramref name="error"/> as the current error and appends to <see cref="All"/>.
    /// The returned disposable restores the previous AsyncLocal value on Dispose — use
    /// <c>using</c> or <c>using var</c> at the call site.
    /// </summary>
    public IDisposable Push(IError error)
    {
        var previous = _current.Value;
        _current.Value = error;
        All.Add(error);
        return new Restorer(previous);
    }

    private sealed class Restorer : IDisposable
    {
        private readonly IError? _previous;
        private bool _disposed;

        public Restorer(IError? previous) { _previous = previous; }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _current.Value = _previous;
        }
    }
}
