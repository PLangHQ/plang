using Data = global::app.data.@this;

namespace app.type.item.clipboard;

/// <summary>
/// PLang <c>clipboard</c> value — what was copied: a text, or a plang value copied whole (a value pasted keeps its
/// type). Between a screen and the host it is what one side copied, for the other to have: it writes itself as
/// <c>{"clipboard": …}</c> (the content as itself — a text its string, a value its own form) and reads back the same.
/// That line is a step on the way: once the screen's own message type carries plang's Data, a clipboard goes as
/// itself.
/// </summary>
[global::app.Attributes.PlangType("clipboard")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "{\"clipboard\": \"copied text\"}";
    public static string Description => "What was copied: a text, or a value copied whole.";
    public static string Shape => "object";

    // what was copied: a text, or a value whole
    private readonly global::app.type.item.@this _content;

    public @this(global::app.type.item.@this content) => _content = content;

    public override bool IsLeaf => false;

    /// <summary>What was copied.</summary>
    [Out] public global::app.type.item.@this Content => _content;

    /// <summary>Hands what was copied to what holds a clipboard: a text as its text, a value as itself.</summary>
    public void Apply(IHolder holder)
    {
        if (_content is global::app.type.item.text.@this text) holder.Copied(text.ToString());
        else holder.Copied(_content);
    }

    /// <summary>A clipboard is its content: a text when what was copied is one.</summary>
    public override string ToString() => _content.ToString() ?? "";

    public override async System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        writer.BeginObject();
        writer.Name("clipboard");
        await _content.Output(writer, mode, context);
        writer.EndObject();
    }

    public override void Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        writer.Name("clipboard");
        _content.Write(writer);
        writer.EndObject();
    }

    /// <summary>A clipboard passes; a text or any other value is what was copied.</summary>
    public static @this? Create(object? raw) => raw switch
    {
        @this clipboard => clipboard,
        global::app.type.item.@this value => new @this(value),
        string text => new @this((global::app.type.item.text.@this)text),
        _ => null,
    };

    public static @this? Create(object? raw, global::app.type.@this? declared, Data data)
    {
        if (Create(raw) is { } made) return made;
        data.Fail(new global::app.error.Error("a clipboard holds what was copied: a text, or a value", "ClipboardInvalid", 400));
        return null;
    }
}

/// <summary>What holds a clipboard — a display, a window: it takes what was copied.</summary>
public interface IHolder
{
    /// <summary>A text was copied.</summary>
    void Copied(string text);

    /// <summary>A value was copied whole.</summary>
    void Copied(global::app.type.item.@this value);
}
