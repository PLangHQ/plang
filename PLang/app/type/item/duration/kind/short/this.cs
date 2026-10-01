namespace app.type.item.duration.kind.@short;

/// <summary>The short form a step says — a number and its unit: <c>200ms</c>, <c>30s</c>, <c>5m</c>, <c>1h</c>,
/// <c>1d</c>. A span is written in its largest whole unit (90s stays <c>90s</c>, 3600s is <c>1h</c>).</summary>
public sealed class @this : kind.@this
{
    public @this() : base("short") { }

    internal override System.TimeSpan? Span(string text)
    {
        var form = System.Text.RegularExpressions.Regex.Match(text, @"^(-?\d+(?:\.\d+)?)\s*(ms|s|m|h|d)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!form.Success) return null;
        var amount = double.Parse(form.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        return form.Groups[2].Value.ToLowerInvariant() switch
        {
            "ms" => System.TimeSpan.FromMilliseconds(amount),
            "s" => System.TimeSpan.FromSeconds(amount),
            "m" => System.TimeSpan.FromMinutes(amount),
            "h" => System.TimeSpan.FromHours(amount),
            _ => System.TimeSpan.FromDays(amount),
        };
    }

    internal override string Text(System.TimeSpan span)
    {
        var ticks = span.Ticks;
        if (ticks == 0) return "0s";
        foreach (var (unit, size) in new[] { ("d", System.TimeSpan.TicksPerDay), ("h", System.TimeSpan.TicksPerHour),
                     ("m", System.TimeSpan.TicksPerMinute), ("s", System.TimeSpan.TicksPerSecond) })
            if (ticks != 0 && ticks % size == 0) return $"{ticks / size}{unit}";
        return $"{span.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}ms";
    }
}
