namespace app.variables;

/// <summary>
/// Identifies a variable by name. Used as the wrapped type in <c>Data&lt;Variable&gt;</c>
/// for action handler parameters that name a variable rather than carry its value
/// (write targets, read-by-name lookups: <c>variable.set</c>, <c>list.add</c>,
/// <c>loop.foreach</c> ItemName/KeyName, etc.).
///
/// <see cref="Resolve(string, Actor.Context.@this)"/> is invoked by the source
/// generator's <c>Data&lt;T&gt;</c> emit through the <c>Data.As&lt;T&gt;</c> raw-name
/// dispatch — see <see cref="IRawNameResolvable"/>. Both <c>"%x%"</c> and bare
/// <c>"x"</c> resolve to the same canonical <c>Name = "x"</c>;
/// <see cref="WasPercentWrapped"/> preserves the LLM-emission shape for future
/// build-time validators.
///
/// Provenance lives on the wrapper — <c>Data&lt;Variable&gt;.Signature</c> when signing
/// lands. Variable itself is a value, not a wrapper.
/// </summary>
public sealed record Variable(string Name, string RawValue, bool WasPercentWrapped) : IRawNameResolvable
{
    /// <summary>
    /// Optional Property suffix captured from the raw reference shape
    /// (<c>%x!cost%</c> sets this to <c>"cost"</c>). Variable.set's run path
    /// routes to <c>Data.Properties[Property]</c> when this is non-null.
    /// </summary>
    public string? Property { get; init; }

    /// <summary>
    /// True when the raw reference is syntactically malformed
    /// (<c>%x!!cost%</c>, <c>%x.y!cost%</c>, <c>%x!a!b%</c>). Property-setter
    /// callsites surface a typed InvalidVariableReference error; ordinary
    /// Get-by-name on a malformed reference NotFound's naturally because
    /// the mangled string was preserved on Name.
    /// </summary>
    public bool IsMalformed { get; init; }

    /// <summary>
    /// Convenience for direct C# composition (tests, App.RunAction):
    /// <c>new Variable("myList")</c> is equivalent to a bare-name slot.
    /// Records' primary constructor is inherited; this overload chains into it.
    /// </summary>
    public Variable(string name) : this(name ?? "", name ?? "", false) { }

    /// <summary>
    /// Implicit conversion to string at any string-expecting boundary
    /// (e.g. <c>Variables.Get(name.Value)</c>). Returns the canonical name.
    /// </summary>
    public static implicit operator string(Variable v) => v.Name;

    /// <summary>
    /// Source-generator convention: types declared as <c>Data&lt;T&gt;</c> on action
    /// properties are constructed from a raw string via this static method. The
    /// <c>Data.As&lt;T&gt;</c> raw-name dispatch reflects to find it (bypassing
    /// <c>%var%</c> substitution because Variable implements <see cref="IRawNameResolvable"/>).
    ///
    /// Symmetry with <c>__StripPercent</c>: both <c>%x%</c> and <c>x</c> produce
    /// <c>Name = "x"</c>. <c>WasPercentWrapped</c> records which form was on the
    /// wire so a future validator can flag bare-name LLM emissions if needed.
    /// </summary>
    public static Variable Resolve(string raw, actor.context.@this context)
    {
        if (string.IsNullOrEmpty(raw))
            return new Variable("", raw ?? "", false);

        var trimmed = raw.Trim('%');
        var wasPercentWrapped = raw.Length >= 2 && raw[0] == '%' && raw[^1] == '%';

        // Strip the negation prefix (`%!flag%`) before scanning for the
        // Property separator. The stripped '!' rides on Name so consumers
        // that read Name as a string see the negation marker intact.
        var hasNegationPrefix = trimmed.StartsWith('!');
        var body = hasNegationPrefix ? trimmed[1..] : trimmed;

        // Property suffix (`%x!cost%`) lives between the identifier and any
        // dot/bracket path. Locate the first path separator first so a '!'
        // that appears AFTER a '.' or '[' is treated as malformed rather
        // than splitting Name into a wrong shape.
        int pathStart = -1;
        for (int i = 0; i < body.Length; i++) { if (body[i] == '.' || body[i] == '[') { pathStart = i; break; } }
        var head = pathStart < 0 ? body : body[..pathStart];
        var tail = pathStart < 0 ? "" : body[pathStart..];

        int bangsInHead = 0, bangsInTail = 0;
        for (int i = 0; i < head.Length; i++) if (head[i] == '!') bangsInHead++;
        for (int i = 0; i < tail.Length; i++) if (tail[i] == '!') bangsInTail++;

        string name;
        string? property = null;
        bool malformed = false;
        if (bangsInTail > 0 || bangsInHead > 1)
        {
            // Multiple bangs or bang after dot — preserve the whole body on
            // Name and flag the shape so property-setter callsites can fail
            // with a typed error. Variable.Resolve itself never throws —
            // it's called deep inside source-generator parameter resolution
            // where an exception manifests as an opaque NRE upstream.
            name = (hasNegationPrefix ? "!" : "") + body;
            malformed = true;
        }
        else if (bangsInHead == 1)
        {
            var bang = head.IndexOf('!');
            name = (hasNegationPrefix ? "!" : "") + head[..bang];
            property = head[(bang + 1)..];
            if (property.Length == 0) { property = null; malformed = true; }
        }
        else
        {
            // No `!` — keep the full body (identifier + any '.'/'[' path)
            // on Name so callers that read Variable.Name as a string see
            // the same reference the LLM emitted. Pre-Stage-4 behaviour.
            name = (hasNegationPrefix ? "!" : "") + body;
        }

        return new Variable(name, raw, wasPercentWrapped) { Property = property, IsMalformed = malformed };
    }

    /// <summary>
    /// String-interpolation friendliness: <c>$"variable '{name.Value}' missing"</c>
    /// reads as the canonical name rather than the synthesized record format.
    /// Matches <see cref="op_Implicit(Variable)"/>'s string semantics.
    /// </summary>
    public override string ToString() => Name;
}
