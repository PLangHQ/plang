namespace app.type.item.sensitive;

/// <summary>
/// PLang <c>sensitive</c> value — characters nobody should see: a key, a password (<c>ask "password?" secretly</c>).
/// It writes itself as <c>****</c> in every view but plang's own store, where it is kept whole, so debug, a trace, a
/// snapshot and a channel show it masked without asking. Code that must send it (a request's key) reads its
/// <see cref="Characters"/>.
/// </summary>
[global::app.Attributes.PlangType("sensitive")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    // what a sensitive value shows
    private const string Masked = "****";

    public static string Shape => "string";

    /// <summary>Characters nobody should see, born holding <paramref name="type"/> — the app's sensitive.</summary>
    public @this(string characters, global::app.type.@this type) : base(type) => Characters = characters;

    /// <summary>The characters, for code that must send them — never shown.</summary>
    internal string Characters { get; }

    public override bool IsLeaf => true;

    public override bool IsTruthy() => Characters.Length > 0;

    /// <summary>A sensitive value says what it is; the writer says how it shows — whole in plang's own store, masked
    /// elsewhere.</summary>
    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter w, global::app.View mode,
        global::app.actor.context.@this? context) { w.Sensitive(Characters); return default; }

    public override string ToString() => Masked;

    /// <summary>A sensitive value passes through; a text or characters become one, born holding the app's type.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data) => raw switch
    {
        @this sensitive => sensitive,
        global::app.type.item.@this { RawText: { } text } => new @this(text, data.Context!.App.type.list[typeof(@this)]),
        string text => new @this(text, data.Context!.App.type.list[typeof(@this)]),
        _ => null,
    };
}
