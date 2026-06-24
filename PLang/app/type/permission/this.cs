using System.Text.RegularExpressions;
using IWriter = global::app.channel.serializer.IWriter;
using Text = global::app.type.text.@this;

namespace app.type.permission;

/// <summary>How a grant's <see cref="@this.Path"/> matches a request path.</summary>
public enum Match
{
    Exact,
    Glob,
    Regex,
}

/// <summary>
/// A single action a grant may cover. The grant holds a SET of these; a request
/// names ONE. Open across domains by intent — filesystem (read/write/delete),
/// execute (load-as-code), and the same four cover http and other schemes
/// uniformly. Execute is opt-in: a Read grant does NOT cover it (Unix r/w/x).
/// </summary>
public enum Verb
{
    Read,
    Write,
    Delete,
    Execute,
}

/// <summary>
/// A signed permission grant or an in-flight request — same shape both ways.
/// <c>Covers</c> answers "does this grant cover that request?". Asymmetry is
/// encoded by <see cref="Match"/> (the grant's path-pattern semantics) and by
/// verb-set containment (grant's <see cref="Verbs"/> ⊇ request's).
///
/// <para>Permission is a fundamental PLang value (an <c>item</c>), not a path
/// thing: it <i>references</i> a resource (a <see cref="Path"/> of any scheme —
/// <c>file://</c>, <c>http://</c>, …) but is an authorization in its own right.
/// It owns its wire form via <see cref="Write"/>/<see cref="Create"/> — no
/// reflection, no Normalize.</para>
///
/// <para>Identity is (Actor + Path + Verbs); the persistence root is the
/// per-actor sqlite store. No App-instance scoping — grants survive
/// <c>new App()</c> on the same root, the contract the "a" ("always allow")
/// answer promises. Time bound: a grant's signature is verified with
/// <c>SkipFreshnessCheck=true</c>, so the wire-freshness window doesn't apply;
/// the grant lives for its signature's <c>Expires</c> (null today = permanent).</para>
/// </summary>
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    [Out, Store] public string Actor { get; }
    [Out, Store] public string Path { get; }
    [Out, Store] public IReadOnlySet<Verb> Verbs { get; }
    [Out, Store] public Match Match { get; }

    public @this(string Actor, string Path, IReadOnlySet<Verb> Verbs, Match Match)
    {
        this.Actor = Actor;
        this.Path = Path;
        this.Verbs = Verbs;
        this.Match = Match;
    }

    /// <summary>Every verb — the "fully granted" set an "a"/"y" answer mints.</summary>
    public static IReadOnlySet<Verb> AllVerbs { get; } =
        new HashSet<Verb> { Verb.Read, Verb.Write, Verb.Delete, Verb.Execute };

    /// <summary>A request for a single verb against one resource.</summary>
    public static @this Request(string actor, string path, Verb verb, Match match = Match.Exact)
        => new(actor, path, new HashSet<Verb> { verb }, match);

    // Value equality — two grants for the same actor/path/verb-set/match are
    // equal, which the permission-table dedup + the round-trip test rely on.
    public override bool Equals(object? obj) => obj is @this o
        && Actor == o.Actor && Path == o.Path && Match == o.Match && Verbs.SetEquals(o.Verbs);
    public override int GetHashCode()
    {
        var h = new System.HashCode();
        h.Add(Actor); h.Add(Path); h.Add(Match);
        foreach (var v in Verbs.OrderBy(v => v)) h.Add(v);
        return h.ToHashCode();
    }

    public bool Covers(@this request) =>
        Actor == request.Actor
        && PathMatches(request.Path)
        && request.Verbs.IsSubsetOf(Verbs);

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

    /// <summary>
    /// The grant owns its wire form: <c>{actor, path, match, verbs:[…]}</c>. No
    /// reflection — the writer never type-switches on it (OBP Rule 9). Symmetric
    /// with <see cref="Create"/>.
    /// </summary>
    public override void Write(IWriter w)
    {
        w.BeginObject();
        w.Name("actor"); w.String(Actor);
        w.Name("path");  w.String(Path);
        w.Name("match"); w.String(Match.ToString());
        w.Name("verbs");
        w.BeginArray(Verbs.Count);
        foreach (var v in Verbs) w.String(v.ToString());
        w.EndArray();
        w.EndObject();
    }

    /// <summary>
    /// Reconstructs a grant from its own <see cref="Write"/> shape (the wire dict
    /// <c>{actor, path, match, verbs:[…]}</c>). Pass-through when the value already
    /// is a grant; declines anything else.
    /// </summary>
    public static @this? Create(global::app.type.item.@this value, global::app.data.@this asking)
    {
        if (value is @this self) return self;
        if (value is not global::app.type.dict.@this dict)
        {
            asking.Fail(new global::app.error.Error(
                $"%{asking.Name}% holds a {value.Mint().Name} — 'permission' cannot be created from it.",
                "CreateItemDeclined", 400));
            return null;
        }

        string actor = dict.Get<Text>("actor")?.ToString() ?? "";
        string path  = dict.Get<Text>("path")?.ToString() ?? "";
        Match match  = System.Enum.TryParse<Match>(dict.Get<Text>("match")?.ToString(), ignoreCase: true, out var m)
            ? m : Match.Exact;

        var verbs = new HashSet<Verb>();
        if (dict.Get("verbs")?.Peek() is global::app.type.list.@this list)
            foreach (var entry in list.Items)
                if (entry.Peek() is Text t && System.Enum.TryParse<Verb>(t.ToString(), ignoreCase: true, out var v))
                    verbs.Add(v);

        return new @this(actor, path, verbs, match);
    }
}
