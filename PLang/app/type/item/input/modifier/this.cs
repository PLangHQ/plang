namespace app.type.item.input.modifier;

/// <summary>
/// A modifier key held — alt, ctrl, meta, shift. It is its name; its code is the screen's wire count for it
/// (DevTools' own), kept inside for the line and the hosts that speak codes.
/// </summary>
[global::app.Attributes.PlangType("modifier")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    private readonly string _name;

    internal @this(string name, int code, global::app.type.@this type) : base(type)
    {
        _name = name;
        Code = code;
    }

    /// <summary>A part of an input, never LLM vocabulary of its own — kept out of the catalog.</summary>
    public static bool Internal => true;

    /// <summary>Its code on the screen's wire.</summary>
    internal int Code { get; }

    public override bool IsLeaf => true;

    /// <summary>A modifier writes its name.</summary>
    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer) { writer.String(_name); return default; }

    /// <summary>A modifier passes; anything else declines — a modifier is made with the input it was held for.</summary>
    public static @this? Create(object? raw) => raw as @this;

    public static @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (raw is @this modifier) return modifier;
        data.Fail(new global::app.error.@this("a modifier is made with the input it was held for (alt, ctrl, meta, shift)", "ModifierInvalid", 400));
        return null;
    }

    public override bool Equals(object? other) => other is @this modifier && modifier.Code == Code;

    public override int GetHashCode() => Code;

    public override string ToString() => _name;
}
