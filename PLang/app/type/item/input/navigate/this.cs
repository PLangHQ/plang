namespace app.type.item.input.navigate;

/// <summary>Where to go.</summary>
[global::app.Attributes.PlangType("navigation")]
public enum Direction { back, forward, reload }

/// <summary>Back, forward or reload — asked by a key (Alt+←, Alt+→, F5) or a mouse's side buttons.</summary>
public sealed class @this : input.@this
{
    /// <summary>A navigation, born with the app's <paramref name="types"/>: its own and its choice's.</summary>
    public @this(global::app.type.list.@this types, Direction to, long? stamp = null) : base(types[typeof(@this)], stamp)
        => To = new(to, types[typeof(global::app.type.item.choice.@this<Direction>)]);

    /// <summary>back, forward or reload.</summary>
    [Out] public global::app.type.item.choice.@this<Direction> To { get; }

    private protected override void Applied(ITarget target) => target.Navigate(this);

    public override void Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        writer.Name("nav"); To.Write(writer);
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
