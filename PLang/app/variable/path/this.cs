namespace app.variable.path;

/// <summary>
/// A navigation path — the parsed form of a reference string like
/// <c>goal.Steps[planStep.index]</c>, <c>user.name</c>, <c>x!file!path</c>, or
/// <c>tags."key.with.dots"</c>. The path OWNS its own tokenization: the string
/// parses ONCE, here, into an ordered list of typed <see cref="Segment"/>s — there
/// is no free-function <c>ParseNextSegment</c> tokenizer to be re-run mid-walk.
///
/// Walking is then just: ask the path for its head (<see cref="Split"/>) and the
/// value to navigate it (<c>app.type.item.@this.Navigate</c>), recursing on the
/// tail. Reads and writes share that walk; only the last segment differs.
///
/// Distinct from <c>app.type.item.path.@this</c> (a filesystem path): this is the path
/// THROUGH a value graph, not a path on disk.
/// </summary>
public sealed class @this
{
    public System.Collections.Generic.IReadOnlyList<Segment> Segments { get; }

    public @this(System.Collections.Generic.IReadOnlyList<Segment> segments) => Segments = segments;

    public bool IsEmpty => Segments.Count == 0;

    /// <summary>Head segment + the remaining path. An empty path returns
    /// <c>(null, this)</c> — the walk terminus.</summary>
    public (Segment? head, @this tail) Split()
        => IsEmpty
            ? (null, this)
            : (Segments[0], Tail);

    /// <summary>The root variable this path descends from — the store key it roots at, the first
    /// segment's source token. A plain member is its name; an infra root keeps its marker
    /// (<c>!app.goal</c> → <c>!app</c>, the key it lives under). Empty for an empty path.</summary>
    public string Root => IsEmpty ? "" : Segments[0].Raw;

    /// <summary>The path after the root — <c>goal.Steps[0]</c> → <c>Steps[0]</c>.
    /// Empty for a bare root (<c>goal</c>).</summary>
    public @this Tail
        => IsEmpty ? this : new @this(new System.ArraySegment<Segment>(ToArray(), 1, Segments.Count - 1));

    /// <summary>All but the last segment — the walk to the leaf's parent
    /// (<c>Steps[0]</c> → <c>Steps</c>). Empty when the path IS the leaf.</summary>
    public @this Parent
        => Segments.Count <= 1 ? new @this(System.Array.Empty<Segment>())
           : new @this(new System.ArraySegment<Segment>(ToArray(), 0, Segments.Count - 1));

    /// <summary>The final segment — the leaf a write lands on.</summary>
    public Segment Last => Segments[Segments.Count - 1];

    private Segment[] ToArray() => Segments as Segment[] ?? System.Linq.Enumerable.ToArray(Segments);

    /// <summary>Parse a reference string into a path. Mirrors the tokens the old
    /// <c>ParseNextSegment</c> + GetChild bracket/quote/method/`!` interpretation
    /// produced — see NavigationPathParityTests.</summary>
    public static @this Parse(string raw)
    {
        var segments = new System.Collections.Generic.List<Segment>();
        var rest = raw ?? "";
        while (rest.Length > 0)
        {
            var (token, remaining) = NextToken(rest);
            if (token.Length == 0) break; // defensive: no progress
            segments.Add(Classify(token));
            rest = remaining;
        }
        return new @this(segments);
    }

    /// <summary>Round-trips to the source reference form. Member/Call segments take a
    /// leading dot (except first); Index and Infra carry their own delimiter.</summary>
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < Segments.Count; i++)
        {
            var seg = Segments[i];
            if (i > 0 && seg is Segment.Member or Segment.Call) sb.Append('.');
            sb.Append(seg.Raw);
        }
        return sb.ToString();
    }

    // --- Tokenizer (the path owns it; replaces the free-function ParseNextSegment) ---

    /// <summary>Splits the next token from a path string, respecting bracket depth,
    /// parentheses (method calls), quotes, and the chain-wide <c>!</c> plane marker.</summary>
    private static (string token, string remaining) NextToken(string path)
    {
        int depth = 0;
        bool inQuote = false;
        char quoteChar = '\0';

        for (int i = 0; i < path.Length; i++)
        {
            var c = path[i];

            if ((c == '"' || c == '\'') && depth <= 1)
            {
                if (!inQuote) { inQuote = true; quoteChar = c; }
                else if (c == quoteChar) { inQuote = false; }
                continue;
            }
            if (inQuote) continue;

            if (c == '(') { depth++; continue; }
            if (c == ')') { depth--; continue; }

            // Split at an open bracket at depth 0: "Steps[0]" → ("Steps", "[0]").
            if (c == '[' && depth == 0 && i > 0) return (path[..i], path[i..]);

            if (c == '[') { depth++; continue; }
            if (c == ']') { depth--; continue; }

            // Dot splits at depth 0 (the dot is consumed — segments don't carry it).
            if (c == '.' && depth == 0) return (path[..i], path[(i + 1)..]);

            // A second `!` starts the next property-plane hop: "!file!path" →
            // ("!file", "!path"). The leading `!` (i == 0) is this segment's own prefix.
            if (c == '!' && depth == 0 && i > 0) return (path[..i], path[i..]);
        }

        return (path, "");
    }

    private static Segment Classify(string token)
    {
        // Bracket index: [expr] — the inner is itself a path (literal or variable).
        if (token.Length >= 2 && token[0] == '[' && token[^1] == ']')
            return new Segment.Index(token, Parse(token[1..^1]));

        // Quoted member: "key" / 'key' — forced literal key, no method/infra parsing.
        if (token.Length >= 2
            && ((token[0] == '"' && token[^1] == '"') || (token[0] == '\'' && token[^1] == '\'')))
            return new Segment.Member(token, token[1..^1], quoted: true);

        // Method call: name(args)
        int paren = token.IndexOf('(');
        if (paren > 0 && token[^1] == ')')
            return new Segment.Call(token, token[..paren], token[(paren + 1)..^1]);

        // Infrastructure plane: !name
        if (token.Length > 0 && token[0] == '!')
            return new Segment.Infra(token, token[1..]);

        // Plain member / dict key.
        return new Segment.Member(token, token, quoted: false);
    }
}
