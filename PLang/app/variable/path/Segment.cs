namespace app.variable.path;

/// <summary>
/// One hop of a navigation <see cref="@this">path</see>. A path string parses
/// into an ordered list of these — the typed replacement for the raw string
/// tokens the old <c>ParseNextSegment</c> free function returned.
///
/// Each kind knows its own source form (<see cref="Raw"/>) so a path round-trips,
/// and carries the structured fields a walker needs — without re-parsing strings
/// mid-walk. A value navigates a segment via <c>app.type.item.@this.Navigate</c>;
/// the segment owns what its key IS (a member name, a resolved bracket index, an
/// infrastructure plane, a method call).
/// </summary>
public abstract class Segment
{
    /// <summary>Exact source token this segment was parsed from (e.g. <c>Steps</c>,
    /// <c>[planStep.index]</c>, <c>!file</c>, <c>grep("p")</c>). Round-trips.</summary>
    public string Raw { get; }

    protected Segment(string raw) => Raw = raw;

    public override string ToString() => Raw;

    /// <summary>A dot member / dict key. <c>Quoted</c> when the source forced a
    /// literal key (<c>tags."key.with.dots"</c>) — bypasses method/infra parsing.</summary>
    public sealed class Member : Segment
    {
        public string Name { get; }
        public bool Quoted { get; }
        public Member(string raw, string name, bool quoted) : base(raw)
        {
            Name = name;
            Quoted = quoted;
        }
    }

    /// <summary>A bracket index <c>[expr]</c>. The inner is ITSELF a path — a
    /// numeric/quoted literal (<c>[0]</c>, <c>["k"]</c>) is a degenerate one-member
    /// path; a variable index (<c>[planStep.index]</c>) is a real path the walker
    /// resolves against the store. No regex, no special case — it's just a path.</summary>
    public sealed class Index : Segment
    {
        public @this Inner { get; }
        public Index(string raw, @this inner) : base(raw) => Inner = inner;
    }

    /// <summary>Infrastructure plane (<c>!file</c>, <c>!path</c>, <c>!data</c>) — reads
    /// the binding (Name/Error/Type/Properties or a property-plane hop), not the value.</summary>
    public sealed class Infra : Segment
    {
        public string Name { get; }
        public Infra(string raw, string name) : base(raw) => Name = name;
    }

    /// <summary>Method-style segment (<c>grep("p")</c>, <c>maxLength(100)</c>).</summary>
    public sealed class Call : Segment
    {
        public string Method { get; }
        public string Args { get; }
        public Call(string raw, string method, string args) : base(raw)
        {
            Method = method;
            Args = args;
        }
    }
}
