namespace app.type.item.duration.@short;

/// <summary>A span in the short form a step says — a number and its unit: <c>200ms</c>, <c>30s</c>, <c>5m</c>,
/// <c>1h</c>, <c>1d</c>. It writes itself in its largest whole unit (90s stays <c>90s</c>, 3600s is <c>1h</c>).</summary>
public sealed class @this : duration.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>A span born holding <paramref name="type"/>, the app's <c>{duration, short}</c>.</summary>
    public @this(System.TimeSpan value, global::app.type.@this type) : base(value, type) { }

    // A span through the C# implicit conversion: its type built from its class, until the implicit-conversion branch
    // removes that conversion.
    internal @this(System.TimeSpan value) : base(value, new global::app.type.@this("duration", typeof(@this))) { }

    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        var ticks = Value.Ticks;
        writer.String(ticks == 0 ? "0s"
            : ticks % System.TimeSpan.TicksPerDay == 0 ? $"{ticks / System.TimeSpan.TicksPerDay}d"
            : ticks % System.TimeSpan.TicksPerHour == 0 ? $"{ticks / System.TimeSpan.TicksPerHour}h"
            : ticks % System.TimeSpan.TicksPerMinute == 0 ? $"{ticks / System.TimeSpan.TicksPerMinute}m"
            : ticks % System.TimeSpan.TicksPerSecond == 0 ? $"{ticks / System.TimeSpan.TicksPerSecond}s"
            : $"{Value.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}ms");
        return default;
    }

    /// <summary>A span, or a text in the short form, as a short duration, born with the app's <c>{duration, short}</c>;
    /// a text in another form declines (<c>null</c>).</summary>
    public static new @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
    {
        var span = (raw is global::app.type.item.@this leaf ? leaf.Clr<object>() : raw) switch
        {
            System.TimeSpan given => given,
            string text when System.Text.RegularExpressions.Regex.Match(text.Trim(), @"^(-?\d+(?:\.\d+)?)\s*(ms|s|m|h|d)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase) is { Success: true } form
                => Unit(double.Parse(form.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), form.Groups[2].Value),
            _ => (System.TimeSpan?)null,
        };
        return span is { } value && declared is { } type ? new(value, type) : null;

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
