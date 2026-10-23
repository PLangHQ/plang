namespace app.type.item.duration.span;

/// <summary>A span with no form of its own (how long a step ran): written in the standard its asker's setting names
/// (<c>short</c> unless set), read once per write. A kind no program names.</summary>
public sealed class @this : duration.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>Never a kind a program names: the builder's kind follow-up and the setting's standards leave it out.</summary>
    public static bool Internal => true;

    /// <summary>A span born holding <paramref name="type"/>, the app's <c>{duration, span}</c>.</summary>
    public @this(System.TimeSpan value, global::app.type.@this type) : base(value, type) { }

    /// <summary>The span in the standard its asker's setting names, as that standard writes it.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        var standard = (await writer.Context.Setting.Get<setting.@this>()).Standard.Value;
        await (writer.Context.App.type.list[new global::app.type.@this("duration", standard.Name), writer.Context]
                .Make(Value, new global::app.data.@this("", context: writer.Context)) as duration.@this
            ?? throw new System.InvalidOperationException($"the duration standard {standard.Name} made no span of {Value}.")).Write(writer);
    }

    /// <summary>A span, or a text in any standard's form (a span written out and read back), as a span born holding
    /// <paramref name="type"/>; anything else declines.</summary>
    public static new @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
        => type is not null && Measure(raw, text => Resolve(text, data.Context)?.Value) is { } value ? new(value, type) : null;
}
