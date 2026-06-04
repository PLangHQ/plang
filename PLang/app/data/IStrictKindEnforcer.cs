namespace app.data;

/// <summary>
/// A value that carries a strict <c>kind</c> requirement and validates it
/// against its own content at the moment the content is available — now if the
/// content is already loaded, or later at the value's own load seam if it is
/// lazy. Sibling to <see cref="IBooleanResolvable"/>: the discipline lives on
/// the value, not at the <c>variable.set</c> call site, so a reference
/// fundamental (image; audio/video follow) self-validates wherever its bytes
/// materialise.
///
/// <para>Why this and not just <see cref="IKindValidatable"/>: the validator is
/// a stateless sniffer (does <em>this</em> byte[] match <em>that</em> kind). The
/// enforcer is the imprint — "when my bytes exist, they must be a gif" — that a
/// path-backed value must remember so <c>BytesAsync</c> can throw on a mismatch
/// the <c>set</c> never saw (Ingi: validate at byte-materialization, throw if
/// strict).</para>
/// </summary>
public interface IStrictKindEnforcer
{
    /// <summary>Imprint the strict kind the value's content must match.</summary>
    void RequireStrictKind(string kind);

    /// <summary>
    /// Sniff the loaded content against the imprinted kind. Returns
    /// <c>(true, null)</c> on match, <c>(false, actualKind)</c> on mismatch, or
    /// <c>null</c> when the content is not loaded yet (enforcement deferred to
    /// the value's own load seam).
    /// </summary>
    (bool ok, string? actualKind)? CheckStrictKind();
}

/// <summary>
/// Thrown at byte-materialization when a strict reference fundamental's loaded
/// content does not match its declared kind (e.g. a PNG behind
/// <c>as image/gif strict</c>). Surfaces at first content access — the
/// <c>set</c> stayed lazy.
/// </summary>
public sealed class StrictKindMismatchException : System.Exception
{
    public string RequiredKind { get; }
    public string? ActualKind { get; }

    public StrictKindMismatchException(string requiredKind, string? actualKind)
        : base($"Strict kind mismatch: declared kind '{requiredKind}'"
            + (actualKind != null ? $" but content is '{actualKind}'." : "."))
    {
        RequiredKind = requiredKind;
        ActualKind = actualKind;
    }
}
