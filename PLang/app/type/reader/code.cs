namespace app.type.reader;

/// <summary>The code's readers — the ones its facts ship, scanned from its assembly on first ask; nothing is under
/// them.</summary>
internal sealed class code : @this
{
    internal code(global::app.type.list.@this.Code code) : base(code, [typeof(@this).Assembly]) { }
}
