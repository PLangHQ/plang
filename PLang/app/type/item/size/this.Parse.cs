namespace app.type.item.size;

/// <summary>
/// String → size. Each standard is a kind of size and a class of its own, reading its own suffixes (<c>iec</c> KiB
/// MiB…, <c>si</c> kB MB…, any case); the suffixes don't overlap, so the text names its standard and the size is born
/// that one. A number with no unit, or with <c>B</c>, is a count of bytes, made by its asker's standard. The standards
/// asked are the app's own kinds of size, each through its own type's make.
/// </summary>
public partial class @this
{
    public static @this? Resolve(string raw, global::app.actor.context.@this context)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();
        var count = System.Text.RegularExpressions.Regex.Match(raw, @"^(\d+)\s*(?:b|bytes?)?$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (count.Success && long.TryParse(count.Groups[1].Value, out var bytes)) return Create(bytes, context);
        foreach (var type in context.App.type.list["size"].kind.list(context).Items())
            if (type.Make(raw, new global::app.data.@this("", context: context)) is @this size) return size;
        return null;
    }
}
