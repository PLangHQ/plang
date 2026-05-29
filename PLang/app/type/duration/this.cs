namespace app.type.duration;

/// <summary>
/// Wrapper for <see cref="System.TimeSpan"/> — the CLR type PLang's
/// <c>duration</c> name resolves to (Stage 6 cleanup: <c>timespan</c>
/// survives as a deprecated alias). PLang devs write prose ("a duration
/// of 5 minutes") and pick types that read like prose.
///
/// <para>Two text forms parse: <c>"1.02:03:04"</c> (TimeSpan canonical)
/// and ISO-8601 duration (<c>"PT5M"</c>, <c>"P1DT2H"</c>).</para>
/// </summary>
public sealed partial class @this
{
    public static string Example => "PT5M";
    public static string Shape => "string";

    public System.TimeSpan Value { get; }

    public @this(System.TimeSpan value) { Value = value; }

    public override string ToString() => Value.ToString();
}
