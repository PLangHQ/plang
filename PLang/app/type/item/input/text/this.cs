namespace app.type.item.input.text;

/// <summary>Text typed — a character, or what an input method composed. Typed, not pressed: what the keys meant.</summary>
public sealed class @this : input.@this
{
    /// <summary>What was <paramref name="typed"/>, born with the app's <paramref name="types"/>.</summary>
    public @this(global::app.type.list.@this types, string typed, long? stamp = null) : base(types[typeof(@this)], stamp)
        => Typed = new global::app.type.item.text.@this(typed ?? "", types["text"]);

    /// <summary>What was typed.</summary>
    [Out] public global::app.type.item.text.@this Typed { get; }

    private protected override void Applied(ITarget target) => target.Text(this);

    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        writer.Name("text"); await Typed.Output(writer);
        await WriteStamp(writer);
        writer.EndObject();
    }
}
