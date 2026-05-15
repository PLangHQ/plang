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
        return new Variable(trimmed, raw, wasPercentWrapped);
    }

    /// <summary>
    /// String-interpolation friendliness: <c>$"variable '{name.Value}' missing"</c>
    /// reads as the canonical name rather than the synthesized record format.
    /// Matches <see cref="op_Implicit(Variable)"/>'s string semantics.
    /// </summary>
    public override string ToString() => Name;
}
