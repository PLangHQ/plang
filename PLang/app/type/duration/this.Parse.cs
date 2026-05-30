namespace app.type.duration;

/// <summary>
/// String → duration. Accepts both CLR <see cref="System.TimeSpan"/> text
/// (<c>"1.02:03:04"</c>, <c>"00:05:00"</c>) and ISO-8601 duration
/// (<c>"PT5M"</c>, <c>"P1DT2H30M"</c>). The ISO path is hand-rolled
/// because System.Xml's parser isn't available everywhere and we want a
/// tight surface here.
/// </summary>
public sealed partial class @this
{
    public static @this? Resolve(string raw, global::app.actor.context.@this context)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();

        if (raw.StartsWith('P') || raw.StartsWith('-') && raw.Length > 1 && raw[1] == 'P')
        {
            var iso = TryParseIso(raw);
            if (iso != null) return new @this(iso.Value);
        }

        return System.TimeSpan.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? new @this(v) : null;
    }

    /// <summary>
    /// Parses an ISO-8601 duration like <c>P1DT2H30M15.5S</c>. Years and
    /// months are rejected (TimeSpan has no calendar awareness).
    /// </summary>
    private static System.TimeSpan? TryParseIso(string s)
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
        var ts = System.TimeSpan.FromDays(days) + System.TimeSpan.FromHours(hours)
               + System.TimeSpan.FromMinutes(minutes) + System.TimeSpan.FromSeconds(seconds);
        return negative ? -ts : ts;
    }
}
