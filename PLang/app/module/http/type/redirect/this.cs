using Data = global::app.data.@this;

namespace app.module.http.type.redirect;

/// <summary>
/// PLang <c>redirect</c> value — how a request follows redirects: <c>{follow: true, max: 10}</c>. Each member left
/// out keeps its default, which lives here once: a request that says nothing about redirects follows up to ten.
/// The http module's own, as crypto's <c>hash</c> lives under crypto.
/// </summary>
[global::app.Attributes.PlangType("redirect")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "{follow: true, max: 10}";
    public static string Description => "How a request follows redirects: whether it follows them, and how many at most.";
    public static string Shape => "object";

    /// <summary>Whether the request follows a redirect — <c>%!http.request.setting.redirect.follow%</c>.</summary>
    [Out, Store] public global::app.type.item.@bool.@this Follow { get; }

    /// <summary>How many redirects it follows at most — <c>%!http.request.setting.redirect.max%</c>.</summary>
    [Out, Store] public global::app.type.item.number.@this Max { get; }

    /// <summary>The defaults: follow, up to ten.</summary>
    public @this() : this(true, 10) { }

    private @this(global::app.type.item.@bool.@this follow, global::app.type.item.number.@this max)
    {
        Follow = follow;
        Max = max;
    }

    public override bool IsLeaf => false;

    /// <summary>A redirect is made from a dict of its members — any left out keeps its default; a member that is
    /// no member of a redirect, or a value it can't take, declines with why.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, Data data)
    {
        if (raw is @this redirect) return redirect;
        if (raw is not global::app.type.item.dict.@this dict)
        {
            data.Fail(new global::app.error.Error("a redirect is {follow, max}", "RedirectInvalid", 400));
            return null;
        }
        var fresh = new @this();
        global::app.type.item.@bool.@this follow = fresh.Follow;
        global::app.type.item.number.@this max = fresh.Max;
        foreach (var entry in dict.Entries(data.Context!))
            switch (entry.Name.ToLowerInvariant())
            {
                case "follow" when entry.Peek() is global::app.type.item.@bool.@this f: follow = f; break;
                case "max" when entry.Peek() is global::app.type.item.number.@this m: max = m; break;
                default:
                    data.Fail(new global::app.error.Error(
                        $"a redirect's members are follow (true or false) and max (a number) — not {entry.Name}", "RedirectInvalid", 400));
                    return null;
            }
        return new @this(follow, max);
    }

    /// <summary>Writes itself as its dict.</summary>
    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        writer.BeginObject();
        writer.Name("follow"); writer.Bool(Follow.Value);
        writer.Name("max"); Max.Write(writer);
        writer.EndObject();
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }

    public override bool Equals(object? obj) => obj is @this other && Follow.Value == other.Follow.Value && Max.Equals(other.Max);
    public override int GetHashCode() => System.HashCode.Combine(Follow.Value, Max.GetHashCode());
}
