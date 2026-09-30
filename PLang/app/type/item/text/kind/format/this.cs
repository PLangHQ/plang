namespace app.type.item.text.kind.format;

/// <summary>
/// A text in another type's format — <c>{text, json}</c>: its characters are json, and it is text. A read keeps it
/// text: it declines <see cref="global::app.type.kind.@this.Parse"/> and <c>Load</c> (the base's), so the text reads
/// itself. Navigation opens it: <see cref="Open"/> hands the characters to the format, parsed only then. Its
/// characters are a token of its format's own writer only where that writer's envelope doesn't carry the value's
/// type: plain json takes them as they are; plang's envelope, which says text of kind json, takes the string.
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    // The format these characters are in — another type's kind (item's json).
    private readonly global::app.type.kind.@this _format;
    private readonly string _owner;

    /// <summary>Text's kind (<paramref name="owner"/> is text's name) for characters in <paramref name="format"/>.</summary>
    public @this(global::app.type.kind.@this format, string owner) : base(format.Name)
    {
        _format = format;
        _owner = owner;
    }

    protected internal override string Owner => _owner;

    /// <summary>A text's characters.</summary>
    public override bool IsText => true;

    /// <summary>Its format's own writer, where the writer's envelope doesn't name the value's type.</summary>
    public override bool Owns(global::app.type.format.IWriter writer) => _format.Owns(writer) && !writer.EmitsSchema;

    /// <summary>The characters as their format reads them — the json they are.</summary>
    public override global::app.type.item.@this? Open(string characters, global::app.actor.context.@this context)
        => _format.Parse(characters, context);
}
