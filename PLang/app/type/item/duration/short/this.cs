namespace app.type.item.duration.@short;

/// <summary>A span in the short form a step says — each unit it has and its number, largest first, as Go writes one:
/// <c>30s</c>, <c>5m</c>, <c>1h30m</c>, <c>2d3h</c>, <c>1m23.456s</c>; under a second, its milliseconds (<c>200ms</c>).
/// A unit it has none of is left out (90s is <c>1m30s</c>, 3600s is <c>1h</c>).</summary>
public sealed class @this : duration.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>A span born holding <paramref name="type"/>, the app's <c>{duration, short}</c>.</summary>
    public @this(System.TimeSpan value, global::app.type.@this type) : base(value, type) { }

    // A span through the C# implicit conversion: its type built from its class, until the implicit-conversion branch
    // removes that conversion.
    internal @this(System.TimeSpan value) : base(value, new global::app.type.@this("duration", typeof(@this))) { }

    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        var span = Value.Duration();
        var sign = Value < System.TimeSpan.Zero ? "-" : "";
        if (span == System.TimeSpan.Zero) writer.String("0s");
        else if (span < System.TimeSpan.FromSeconds(1))
            writer.String(sign + span.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + "ms");
        else
        {
            var text = new System.Text.StringBuilder(sign);
            if (span.Days > 0) text.Append(span.Days).Append('d');
            if (span.Hours > 0) text.Append(span.Hours).Append('h');
            if (span.Minutes > 0) text.Append(span.Minutes).Append('m');
            var seconds = span.Ticks % System.TimeSpan.TicksPerMinute;
            if (seconds > 0)
                text.Append(((decimal)seconds / System.TimeSpan.TicksPerSecond).ToString("0.#######", System.Globalization.CultureInfo.InvariantCulture)).Append('s');
            writer.String(text.ToString());
        }
        return default;
    }

    /// <summary>A span, or a text in the short form, as a short duration born holding <paramref name="type"/>; a
    /// text in another form is refused, naming how short is written.</summary>
    public static new @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
    {
        if (type is not null && Measure(raw, Read) is { } value) return new(value, type);
        Refuse(raw, "short", "30s", data);
        return null;

        // each of its units and their number, in any order they're written (1h30m, 1m23.456s, 200ms), summed
        static System.TimeSpan? Read(string text)
        {
            var form = System.Text.RegularExpressions.Regex.Match(text, @"^(-?)(?:(\d+(?:\.\d+)?)\s*(ms|d|h|m|s)\s*)+$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!form.Success) return null;
            var span = System.TimeSpan.Zero;
            for (var i = 0; i < form.Groups[2].Captures.Count; i++)
                span += Unit(double.Parse(form.Groups[2].Captures[i].Value, System.Globalization.CultureInfo.InvariantCulture),
                    form.Groups[3].Captures[i].Value);
            return form.Groups[1].Value == "-" ? -span : span;
        }

        static System.TimeSpan Unit(double amount, string unit) => unit.ToLowerInvariant() switch
        {
            "ms" => System.TimeSpan.FromMilliseconds(amount),
            "s" => System.TimeSpan.FromSeconds(amount),
            "m" => System.TimeSpan.FromMinutes(amount),
            "h" => System.TimeSpan.FromHours(amount),
            _ => System.TimeSpan.FromDays(amount),
        };
    }
}
