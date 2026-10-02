namespace app.type.item.input.text;

/// <summary>Text typed — a character, or what an input method composed. Typed, not pressed: what the keys meant.</summary>
public sealed class @this : input.@this
{
    private readonly string _typed;

    public @this(string typed, long? stamp = null) : base(stamp) => _typed = typed ?? "";

    private protected override string Variant => "text";

    /// <summary>What was typed.</summary>
    [Out] public global::app.type.item.text.@this Typed => _typed;

    private protected override void Applied(ITarget target) => target.Text(_typed);

    public override void Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        writer.Name("text"); writer.String(_typed);
        WriteStamp(writer);
        writer.EndObject();
    }

    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        Write(writer);
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
}
