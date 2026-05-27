namespace app.types.path;

/// <summary>
/// Pure path-string derivation verbs — no IO, no async, no AuthGate. Each
/// returns a new Path of the same scheme and the same Context. Scheme-aware
/// implementations live on the subclass (FilePath uses OS path semantics;
/// HttpPath uses URL semantics). Held abstract on the base so a future scheme
/// can't silently inherit a wrong implementation.
/// </summary>
public abstract partial class @this
{
    /// <summary>
    /// The containing directory. <c>/Cache/Start.goal</c> → <c>/Cache</c>. Root
    /// returns itself (no further parent) — never throws, never null. Excluded
    /// from default JSON serialization: at root <c>Parent === this</c>, and
    /// callers that serialize a Path via reflection (no PathJsonConverter
    /// registered for the field) would recurse to STJ's max-depth limit.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public abstract @this Parent { get; }

    /// <summary>
    /// Same parent directory, different filename. <c>/Cache/Start.goal</c> +
    /// <c>"Other.goal"</c> → <c>/Cache/Other.goal</c>. Pure transformation —
    /// not a search.
    /// </summary>
    public abstract @this WithName(string name);

    /// <summary>
    /// Same filename stem, different extension. <c>/Cache/Start.goal</c> +
    /// <c>".pr"</c> → <c>/Cache/Start.pr</c>. <c>/foo</c> + <c>".txt"</c> →
    /// <c>/foo.txt</c>. Pure transformation — not a search.
    /// </summary>
    public abstract @this WithExtension(string extension);

    /// <summary>
    /// Append a child segment. <c>/Cache</c> + <c>"Start.goal"</c> →
    /// <c>/Cache/Start.goal</c>.
    /// </summary>
    public abstract @this Combine(string child);

    /// <summary>
    /// Insert a folder between parent and filename. <c>/Cache/Start.goal</c> +
    /// <c>".build"</c> → <c>/Cache/.build/Start.goal</c>.
    /// </summary>
    public abstract @this InFolder(string folder);
}
