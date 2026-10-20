namespace app.type.item.duration.iso;

/// <summary>A span in ISO 8601 — <c>PT30S</c>, <c>P1DT2H30M</c>. Years and months are refused: a span has no
/// calendar.</summary>
public sealed class @this : duration.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>A span born holding <paramref name="type"/>, the app's <c>{duration, iso}</c>.</summary>
    public @this(System.TimeSpan value, global::app.type.@this type) : base(value, type) { }

    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.String(System.Xml.XmlConvert.ToString(Value));
        return default;
    }

    /// <summary>A span, or a text in ISO 8601, as an iso duration born holding <paramref name="declared"/>; a text in
    /// another form is refused, naming how iso is written.</summary>
    public static new @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (declared is { } type && Span(raw, Read) is { } value) return new(value, type);
        Refuse(raw, "iso", "PT30S", data);
        return null;

        static System.TimeSpan? Read(string s)
        {
            bool negative = s.StartsWith('-');
            if (negative) s = s[1..];
            if (s.Length < 2 || s[0] != 'P') return null;
            s = s[1..];
            double days = 0, hours = 0, minutes = 0, seconds = 0;
            bool inTime = false;
            int i = 0;
            while (i < s.Length)
            {
                if (s[i] == 'T') { inTime = true; i++; continue; }
                int start = i;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                if (i == start || i >= s.Length) return null;
                if (!double.TryParse(s[start..i], System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var n)) return null;
                switch (s[i])
                {
                    case 'Y':
                    case 'M' when !inTime:
                        return null; // calendar units unsupported
                    case 'W': days += n * 7; break;
                    case 'D': days += n; break;
                    case 'H' when inTime: hours += n; break;
                    case 'M' when inTime: minutes += n; break;
                    case 'S' when inTime: seconds += n; break;
                    default: return null;
                }
                i++;
            }
            var span = System.TimeSpan.FromDays(days) + System.TimeSpan.FromHours(hours)
                     + System.TimeSpan.FromMinutes(minutes) + System.TimeSpan.FromSeconds(seconds);
            return negative ? -span : span;
        }
    }
}
