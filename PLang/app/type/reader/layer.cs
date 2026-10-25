namespace app.type.reader;

/// <summary>A layer's readers — only what its app registers, over the readers under it: what this one doesn't
/// answer, they do. It scans nothing: the shipped readers are the code's.</summary>
internal sealed class layer : @this
{
    // The readers of the list this layer's list is over.
    private readonly @this _under;

    internal layer(@this under) => _under = under;

    // a layer scans nothing: the readers shipped are the code's, read through the readers under it
    private protected override shipped Shipped { get; } = new();

    private protected override Read? Payload(string typeName, string? kind)
        => base.Payload(typeName, kind) ?? _under.Of(typeName, kind);

    private protected override ITypeReader? Token(string typeName, string? kind)
        => base.Token(typeName, kind) ?? _under.Typed(typeName, kind);

    private protected override string? Owner(string kind) => base.Owner(kind) ?? _under.TypeOf(kind);
}
