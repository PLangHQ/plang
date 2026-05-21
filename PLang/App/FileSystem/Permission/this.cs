using System.Text.RegularExpressions;
using Microsoft.Extensions.FileSystemGlobbing;

namespace App.FileSystem.Permission;

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
/// </summary>
public sealed record @this(
    string Actor,
    string Path,
    Verb.@this Verb,
    Match Match)
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

    private static bool GlobMatches(string pattern, string candidate)
    {
        var matcher = new Matcher(StringComparison.Ordinal);
        matcher.AddInclude(pattern.TrimStart('/'));
        var result = matcher.Match(candidate.TrimStart('/'));
        return result.HasMatches;
    }

    private static bool RegexMatches(string pattern, string candidate)
    {
        try { return Regex.IsMatch(candidate, pattern); }
        catch (ArgumentException) { return false; }
    }
}
