using IWriter = global::app.type.format.IWriter;
using Text = global::app.type.item.text.@this;

namespace app.type.item.permission;

/// <summary>How a grant's <see cref="@this.Path"/> matches a request path. Lowercase: a choice writes its member as
/// the step says it.</summary>
public enum Match
{
    exact,
    glob,
}

/// <summary>
/// A single action a grant may cover. The grant holds a SET of these; a request
/// names ONE. Open across domains by intent — filesystem (read/write/delete),
/// execute (load-as-code), and the same four cover http and other schemes
/// uniformly. Execute is opt-in: a read grant does NOT cover it (Unix r/w/x).
/// Lowercase: the verb says itself, as the step says it.
/// </summary>
public enum Verb
{
    read,
    write,
    delete,
    execute,
}

/// <summary>
/// A signed permission grant or an in-flight request — same shape both ways.
/// <c>Allows</c> answers "does this grant allow that request?". Asymmetry is
/// encoded by <see cref="Match"/> (the grant's path-pattern semantics) and by
/// verb-set containment (grant's <see cref="Verbs"/> ⊇ request's).
///
/// <para>Permission is a fundamental PLang value (an <c>item</c>), not a path
/// thing: it <i>references</i> a resource (a <see cref="Path"/> of any scheme —
/// <c>file://</c>, <c>http://</c>, …) but is an authorization in its own right.
/// It owns its wire form via <see cref="Output"/>/<see cref="Create"/> — no
/// reflection, no Normalize.</para>
///
/// <para>Identity is (Actor + Path + Verbs); the persistence root is the
/// per-actor sqlite store. No App-instance scoping — grants survive
/// <c>new App()</c> on the same root, the contract the "a" ("always allow")
/// answer promises. Time bound: a grant's signature is read from plang's own store, so it gets no live
/// window; the grant lives for the <c>Expires</c> its signer signed (null today = permanent).</para>
/// </summary>
[global::app.Attributes.PlangType("permission")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Shape => "object";
    /// <summary>What a permission is offered in a step: that the step gives one — the writer fills its
    /// <c>{path, verbs}</c> from the step's words.</summary>
    public static IReadOnlyList<global::app.type.item.@this> Offer => [global::app.type.item.given.@this.Instance];

    [Out, Store] public string Actor { get; }
    [Out, Store] public string Path { get; }
    [Out, Store] public IReadOnlySet<Verb> Verbs { get; }
    [Out, Store] public Match Match { get; }

    /// <summary>A grant, born holding <paramref name="type"/> — the app's permission.</summary>
    public @this(string Actor, string Path, IReadOnlySet<Verb> Verbs, Match Match, global::app.type.@this type) : base(type)
    {
        this.Actor = Actor;
        this.Path = Path;
        this.Verbs = Verbs;
        this.Match = Match;
    }

    /// <summary>Every verb — the "fully granted" set an "a"/"y" answer mints.</summary>
    public static IReadOnlySet<Verb> AllVerbs { get; } =
        new HashSet<Verb> { Verb.read, Verb.write, Verb.delete, Verb.execute };

    /// <summary><paramref name="actor"/>'s request for a single verb against one resource.</summary>
    public static @this Request(global::app.actor.@this actor, string path, Verb verb, Match match = Match.exact)
        => new(actor.Name, path, new HashSet<Verb> { verb }, match, actor.App.type.list[typeof(@this)]);

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

    public bool Allows(@this request) =>
        Actor == request.Actor
        && PathMatches(request.Path)
        && request.Verbs.IsSubsetOf(Verbs);

    // A grant compares as its place does, exact or glob alike: a file's place as the OS's disk names it (path.Holds' one
    // rule, case aside where the disk is), so it is never stricter than the containment the gate asks; any other
    // scheme's (a URL's path counts case) as written.
    private bool PathMatches(string requestPath) => Match switch
    {
        Match.exact => string.Equals(Path, requestPath, Comparison),
        Match.glob  => Glob(requestPath),
        _ => false,
    };

    private StringComparison Comparison
        => global::app.type.item.path.scheme.@this.ParseScheme(Path) is "" or "file"
            ? global::app.type.item.path.@this.RootComparison
            : StringComparison.Ordinal;

    /// <summary>
    /// Whether the grant's glob <see cref="Path"/> covers <paramref name="candidate"/>, for file paths
    /// (<c>/apps/*/file.txt</c>) and URLs (<c>https://api.example.com/*</c>) alike. <c>*</c> is any run within one
    /// part; <c>**</c> any run across parts; <c>?</c> one character of a part. A part ends at each of the OS's
    /// separators (<c>/</c> on Linux; <c>\</c> and <c>/</c> on Windows). The literal runs between wildcards compare
    /// with the grant's <see cref="Comparison"/>, the exact grant's own, so a glob never folds case wider than it.
    /// One pass per piece of the glob over the places the candidate has reached: no backtracking.
    /// </summary>
    private bool Glob(string candidate)
    {
        var comparison = Comparison;
        var reach = new bool[candidate.Length + 1];
        reach[0] = true;
        for (int at = 0; at < Path.Length;)
        {
            var next = new bool[candidate.Length + 1];
            if (Path[at] == '*' && at + 1 < Path.Length && Path[at + 1] == '*')
            {
                // across parts: every place from the first one reached
                var from = System.Array.IndexOf(reach, true);
                if (from < 0) return false;
                for (int p = from; p <= candidate.Length; p++) next[p] = true;
                at += 2;
            }
            else if (Path[at] == '*')
            {
                // within a part: a reached place, or one more character that isn't a separator
                for (int p = 0; p <= candidate.Length; p++)
                    next[p] = reach[p] || (p > 0 && next[p - 1] && !IsSeparator(candidate[p - 1]));
                at++;
            }
            else if (Path[at] == '?')
            {
                for (int p = 0; p < candidate.Length; p++)
                    if (reach[p] && !IsSeparator(candidate[p])) next[p + 1] = true;
                at++;
            }
            else
            {
                var end = Path.IndexOfAny(['*', '?'], at);
                var literal = Path.AsSpan(at, (end < 0 ? Path.Length : end) - at);
                for (int p = 0; p + literal.Length <= candidate.Length; p++)
                    if (reach[p] && candidate.AsSpan(p, literal.Length).Equals(literal, comparison)) next[p + literal.Length] = true;
                at += literal.Length;
            }
            reach = next;
        }
        return reach[candidate.Length];

        static bool IsSeparator(char c)
            => c == global::app.Utils.PathHelper.DirectorySeparatorChar || c == global::app.Utils.PathHelper.AltDirectorySeparatorChar;
    }

    /// <summary>A grant writes its own form in every view.</summary>
    /// <summary>
    /// The grant owns its wire form: <c>{actor, path, match, verbs:[…]}</c>, in every view. No
    /// reflection — the writer never type-switches on it (OBP Rule 9). Symmetric
    /// with <see cref="Create"/>.
    /// </summary>
    protected internal override System.Threading.Tasks.ValueTask Write(IWriter w)
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
        return default;
    }

    /// <summary>
    /// Reconstructs a grant from its own <see cref="Output"/> shape (the wire dict
    /// <c>{actor, path, match, verbs:[…]}</c>). Pass-through when the value already
    /// is a grant; declines anything else.
    /// </summary>
    public static @this? Create(object? value, global::app.type.@this? type, global::app.data.@this data)
    {
        if (value is @this self) return self;
        if (value is not global::app.type.item.dict.@this dict)
        {
            data.Fail(new global::app.error.@this(
                $"%{data.Name}% holds a {((value as global::app.type.item.@this)?.Type.Name ?? value?.GetType().Name)} — 'permission' cannot be created from it.",
                "CreateItemDeclined", 400));
            return null;
        }

        var context = data.Context;
        string actor = dict.Get<Text>("actor", context)?.ToString() ?? "";
        string path  = dict.Get<Text>("path", context)?.ToString() ?? "";
        Match match  = System.Enum.TryParse<Match>(dict.Get<Text>("match", context)?.ToString(), ignoreCase: true, out var m)
            ? m : Match.exact;

        var verbs = new HashSet<Verb>();
        if (dict.Get("verbs", context)?.Peek() is global::app.type.item.list.@this list)
            foreach (var entry in list.Items(context))
                if (entry.Peek() is Text t && System.Enum.TryParse<Verb>(t.ToString(), ignoreCase: true, out var v))
                    verbs.Add(v);

        return new @this(actor, path, verbs, match, context!.App.type.list[typeof(@this)]);
    }
}
