namespace app.type.item.path;

/// <summary>
/// Marks a class as a Path-scheme handler for the named scheme. Stage 4 — the
/// attribute is defined here but not yet consumed at runtime; built-in schemes
/// register explicitly at App startup. Future <c>code.load</c> will discover
/// external scheme handlers by reflecting <see cref="PathSchemeAttribute"/>
/// from a loaded DLL.
/// </summary>
/// <remarks>
/// Classes decorated with <see cref="PathSchemeAttribute"/> MUST expose a
/// public static factory
/// <c>Resolve(string raw, actor.context.@this context)</c> returning the
/// scheme's Path subclass. The registered scheme factories call <c>Resolve</c>,
/// and the future-reflection registration path discovers it the same way.
/// <c>Resolve</c> — not a bare single-string constructor — is the contract
/// because it performs the goal-relative resolution and path normalization a
/// raw ctor would skip (a ctor-minted Path would be un-normalized). Built-ins
/// today (<c>FilePath</c>, <c>HttpPath</c>) expose this static.
///
/// <see cref="AllowMultiple"/> is true: <c>HttpPath</c> carries
/// <c>[PathScheme("http")]</c> and <c>[PathScheme("https")]</c> together.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class PathSchemeAttribute : Attribute
{
    public string Scheme { get; }

    public PathSchemeAttribute(string scheme)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheme);
        Scheme = scheme;
    }
}
