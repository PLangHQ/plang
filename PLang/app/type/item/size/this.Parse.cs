namespace app.type.item.size;

/// <summary>
/// String → size. Each standard reads its own suffixes (<c>kind/iec</c> KiB MiB…, <c>kind/si</c> kB MB…, any case); the
/// suffixes don't overlap, so the text names its kind and the size is born with it. A number with no unit, or with
/// <c>B</c>, is a count of bytes: written in its asker's standard, or the setting's default with no asker.
/// </summary>
public sealed partial class @this
{
    public static @this? Resolve(string raw, global::app.actor.context.@this? context)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();
        var count = System.Text.RegularExpressions.Regex.Match(raw, @"^(\d+)\s*(?:b|bytes?)?$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (count.Success && long.TryParse(count.Groups[1].Value, out var bytes))
            return context != null ? new @this(bytes, context) : new @this(bytes);
        foreach (kind.@this standard in new kind.@this[] { new kind.iec.@this(), new kind.si.@this() })
            if (standard.Bytes(raw) is { } inIt) return new @this(inIt, standard);
        return null;
    }
}
