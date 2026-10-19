namespace app.type.item.duration;

/// <summary>
/// String → duration. Each standard a duration is written in is a kind of it (<c>kind/short</c> 30s,
/// <c>kind/iso</c> PT30S, <c>kind/dotnet</c> 00:00:30) and reads its own form; the forms don't overlap, so the
/// text names its kind and the duration is born with it. The standards asked are the app's own kinds of duration,
/// each through its own parse.
/// </summary>
public sealed partial class @this
{
    public static @this? Resolve(string raw, global::app.actor.context.@this context)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();
        foreach (var type in context.App.type.list["duration"].kind.list(context).Items())
            if (type.kind.Parse(raw, context) is @this span) return span;
        return null;
    }
}
