using System.Text.RegularExpressions;

namespace app.type.path.permission;

public enum Match
{
    Exact,
    Glob,
    Regex,
}

/// <summary>
/// A signed permission grant or an in-flight request — same shape both ways.
/// <c>Covers</c> answers "does this grant cover that request?". Asymmetry is
/// encoded by <see cref="Match"/> (grant's pattern semantics) and the verb's
/// sub-options (grant ≥ request, per option).
///
/// Identity is (Actor + Path + Verb); the persistence root is the per-actor
/// sqlite store. No App-instance scoping — grants survive `new App()` on the
/// same root, which is the contract the "a" ("always allow") answer promises.
///
/// Time bound: a grant's signature is verified with
/// <c>SkipFreshnessCheck=true</c>, so the wire-freshness <c>Created+TimeoutMs</c>
/// window doesn't apply. The grant lives for its signature's <c>Expires</c>
/// field — null today (permanent), parameterised later.
/// </summary>
public sealed record @this(
    [property: Out, Store] string Actor,
    [property: Out, Store] string Path,
    [property: Out, Store] verb.@this Verb,
    [property: Out, Store] Match Match)
{
    public bool Covers(@this request) =>
        Actor == request.Actor
        && PathMatches(request.Path)
        && Verb.Covers(request.Verb);

    private bool PathMatches(string requestPath) => Match switch
    {
        Match.Exact => string.Equals(Path, requestPath, StringComparison.Ordinal),
        Match.Glob  => GlobMatches(Path, requestPath),
        Match.Regex => RegexMatches(Path, requestPath),
        _ => false,
    };

    /// <summary>
    /// Glob match over the canonical-form string. Works uniformly for file
    /// paths (<c>/apps/*/file.txt</c>) and URLs (<c>https://api.example.com/*</c>)
    /// — the FileSystemGlobbing matcher chokes on the <c>://</c> in URLs, so the
    /// pattern is compiled to a regex instead. <c>*</c> matches within a single
    /// segment (no <c>/</c>); <c>**</c> matches across segments; <c>?</c> is a
    /// single non-slash char.
    /// </summary>
    private static bool GlobMatches(string pattern, string candidate)
    {
        var rx = new System.Text.StringBuilder("^");
        for (int i = 0; i < pattern.Length; i++)
        {
            char c = pattern[i];
            if (c == '*')
            {
                if (i + 1 < pattern.Length && pattern[i + 1] == '*')
                {
                    rx.Append(".*");
                    i++;
                }
                else
                {
                    rx.Append("[^/]*");
                }
            }
            else if (c == '?')
            {
                rx.Append("[^/]");
            }
            else
            {
                rx.Append(Regex.Escape(c.ToString()));
            }
        }
        rx.Append('$');
        try { return Regex.IsMatch(candidate, rx.ToString()); }
        catch (ArgumentException) { return false; }
    }

    private static bool RegexMatches(string pattern, string candidate)
    {
        try { return Regex.IsMatch(candidate, pattern); }
        catch (ArgumentException) { return false; }
    }
}
