namespace app.types.datetime;

/// <summary>
/// Wrapper for <see cref="System.DateTimeOffset"/> — the CLR type PLang's
/// <c>datetime</c> name now resolves to (Stage 6 cleanup: <c>DateTime</c>
/// is banished from PLang type bindings; the wire is tz-aware end to end).
///
/// <para>The wrapper exists primarily so the catalog renders <c>datetime</c>
/// as a scalar with a folder home for <c>Parse</c>. Most use sites still
/// route through the raw CLR type via <see cref="primitives.@this"/>'s
/// alias table — the @this class doesn't replace the binding, it documents
/// the parse/format home.</para>
/// </summary>
public sealed partial class @this
{
    public static string Example => "2024-03-15T10:30:00+00:00";
    public static string Shape => "string";

    public System.DateTimeOffset Value { get; }

    public @this(System.DateTimeOffset value) { Value = value; }

    public override string ToString() => Value.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
}
