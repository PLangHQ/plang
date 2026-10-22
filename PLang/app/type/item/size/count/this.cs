namespace app.type.item.size.count;

/// <summary>A count of bytes with no standard of its own (a file's length, a download's bytes): its text is in the
/// standard its asker's setting names, read once per write. A kind no program names.</summary>
public sealed class @this : size.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>Never a kind a program names: the builder's kind follow-up and the setting's standards leave it out.</summary>
    public static bool Internal => true;

    /// <summary>A count born holding <paramref name="type"/>, the app's <c>{size, count}</c>.</summary>
    public @this(long bytes, global::app.type.@this type) : base(bytes, type) { }

    /// <summary>Its text in the standard <paramref name="context"/>'s setting names — <c>95.4 MiB</c>, <c>100 MB</c>.</summary>
    public override async System.Threading.Tasks.ValueTask<string> Text(global::app.actor.context.@this context)
    {
        var standard = (await context.Setting.Get<setting.@this>()).Standard.Value;
        return await (context.App.type.list[new global::app.type.@this("size", standard.Name), context]
                .Make(Value, new global::app.data.@this("", context: context)) as size.@this
            ?? throw new System.InvalidOperationException($"the size standard {standard.Name} made no size of {Value} bytes.")).Text(context);
    }

    /// <summary>A count, or a text in any standard's units (a size written out and read back), as a count born holding
    /// <paramref name="type"/>; anything else declines.</summary>
    public static new @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
        => type is not null && ((raw is global::app.type.item.@this leaf ? leaf.Clr<object>() : raw) switch
            {
                long bytes => bytes,
                int bytes => bytes,
                string text => Resolve(text, data.Context)?.Value,
                _ => (long?)null,
            }) is { } count ? new(count, type) : null;
}
