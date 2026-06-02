namespace app.type.number;

public sealed partial class @this
{
    /// <summary>
    /// OBP: <c>number</c> owns how a numeric value is built. <paramref name="kind"/>
    /// picks the CLR precision (<c>int</c>/<c>long</c>/<c>decimal</c>/<c>double</c>/
    /// <c>float</c>); a null kind derives precision from the value's literal shape
    /// via <see cref="Build"/>. Parsing is invariant-culture — a JSON-shaped
    /// <c>"3.14"</c> reads identically regardless of the user's locale.
    ///
    /// <para>Output is the CLR numeric (the alias target downstream expects), not a
    /// <see cref="@this"/> wrapper. The conversion logic lives here; whether the
    /// wrapper becomes the flowing value is a separate decision.</para>
    /// </summary>
    public static global::app.data.@this Convert(object? value, string? kind,
        global::app.actor.context.@this context)
    {
        if (value is null) return global::app.data.@this.Ok(value);

        var target = KindToClr(kind) ?? KindToClr(Build(value));
        if (target == null)
            return global::app.data.@this.FromError(new global::app.error.Error(
                $"Cannot convert {value.GetType().Name} to number.", "NumberConversionFailed", 400)
                { FixSuggestion = "Expected an integer or decimal literal (e.g. 42, 3.14)." });

        try
        {
            return global::app.data.@this.Ok(
                System.Convert.ChangeType(value, target, System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
        {
            return global::app.data.@this.FromError(new global::app.error.Error(
                $"Cannot convert '{value}' to {kind ?? "number"}: {ex.Message}",
                "NumberConversionFailed", 400) { Exception = ex });
        }
    }

    private static System.Type? KindToClr(string? kind) => kind switch
    {
        "int" => typeof(int),
        "long" => typeof(long),
        "decimal" => typeof(decimal),
        "double" => typeof(double),
        "float" => typeof(float),
        _ => null
    };
}
