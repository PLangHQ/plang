namespace app.type.reader;

/// <summary>The code's readers — the ones its facts ship, scanned from its assembly on first ask; nothing is under
/// them.</summary>
internal sealed class code : @this
{
    // The readers the code's assembly ships, made once, the first time any is asked for.
    private readonly Lazy<shipped> _shipped;

    internal code(global::app.type.list.@this.Code code)
        => _shipped = new(() => new shipped(code, [typeof(@this).Assembly]), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

    private protected override shipped Shipped => _shipped.Value;
}
