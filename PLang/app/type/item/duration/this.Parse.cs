namespace app.type.item.duration;

/// <summary>
/// String → duration. Each standard a duration is written in is a kind of it (<c>kind/short</c> 30s,
/// <c>kind/iso</c> PT30S, <c>kind/dotnet</c> 00:00:30) and reads its own form; the forms don't overlap, so the
/// text names its kind and the duration is born with it.
/// </summary>
public sealed partial class @this
{
    public static @this? Resolve(string raw, global::app.actor.context.@this context)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();
        foreach (kind.@this standard in new kind.@this[] { new kind.@short.@this(), new kind.iso.@this(), new kind.dotnet.@this() })
            if (standard.Span(raw) is { } span) return new @this(span, standard);
        return null;
    }
}
