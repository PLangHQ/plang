namespace app.types.path;

/// <summary>
/// Marks a class as a Path-scheme handler for the named scheme. Stage 4 — the
/// attribute is defined here but not yet consumed at runtime; built-in schemes
/// register explicitly at App startup. Future <c>code.load</c> will discover
/// external scheme handlers by reflecting <see cref="PathSchemeAttribute"/>
/// from a loaded DLL.
/// </summary>
/// <remarks>
/// Classes decorated with <see cref="PathSchemeAttribute"/> MUST expose a
/// public single-string constructor (<c>public @this(string raw)</c>) — or the
/// future-reflection registration path won't be able to mint them. Built-ins
/// today (<c>FilePath</c>, <c>HttpPath</c>) honour this signature.
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
