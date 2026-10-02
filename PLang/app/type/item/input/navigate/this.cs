namespace app.type.item.input.navigate;

/// <summary>Where to go.</summary>
public enum Direction { back, forward, reload }

/// <summary>Back, forward or reload — asked by a key (Alt+←, Alt+→, F5) or a mouse's side buttons.</summary>
public sealed class @this : input.@this
{
    private readonly Direction _to;

    public @this(Direction to, long? stamp = null) : base(stamp) => _to = to;

    private protected override string Variant => "navigate";

    /// <summary>back, forward or reload.</summary>
    [Out] public global::app.type.item.text.@this To => _to.ToString();

    private protected override void Applied(ITarget target) => target.Navigate(_to);

    public override void Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        writer.Name("nav"); writer.String(_to.ToString());
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
