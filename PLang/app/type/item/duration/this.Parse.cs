namespace app.type.item.duration;

/// <summary>
/// String → duration. Each standard a duration is written in is a kind of it and a class of its own
/// (<c>short</c> 30s, <c>iso</c> PT30S, <c>dotnet</c> 00:00:30), each reading its own form; the forms don't overlap,
/// so the text names its standard and the duration is born that one. The standards asked are the app's own kinds of
/// duration a text is written in (never a span, which has no form of its own), each through its own type's make.
/// </summary>
public abstract partial class @this
{
    public static @this? Resolve(string raw, global::app.actor.context.@this context)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();
        foreach (var type in context.App.type.list["duration"].kind.list(context).Items().Where(type => !type.Internal))
            if (type.Make(raw, new global::app.data.@this("", context: context)) is @this span) return span;
        return null;
    }
}
