namespace app.type.item.secret;

/// <summary>
/// PLang <c>secret</c> value — characters nobody should see: a key, a password (<c>ask "password?" secretly</c>). It
/// writes itself as <c>****</c> in every view but plang's own store, where it is kept whole, so debug, a trace, a
/// snapshot and a channel show it masked without asking. Code that must send it (a request's key) reads its
/// <see cref="Characters"/>.
/// </summary>
[global::app.Attributes.PlangType("secret")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    // what a secret shows
    private const string Masked = "****";

    public static string Shape => "string";

    public @this(string characters) => Characters = characters;

    /// <summary>The characters, for code that must send them — never shown.</summary>
    internal string Characters { get; }

    public override bool IsLeaf => true;

    public override bool IsTruthy() => Characters.Length > 0;

    public override void Write(global::app.type.format.IWriter w) => w.String(Masked);

    /// <summary>Whole in plang's own store, masked everywhere else.</summary>
    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        writer.String(mode == global::app.View.Store ? Characters : Masked);
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }

    public override string ToString() => Masked;

    /// <summary>A secret passes through; a text or characters become one.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data) => raw switch
    {
        @this secret => secret,
        global::app.type.item.@this { RawText: { } text } => new @this(text),
        string text => new @this(text),
        _ => null,
    };
}
