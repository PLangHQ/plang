namespace app.type.item.input.mouse;

/// <summary>What the mouse did: moved, a button down or up, the wheel turned.</summary>
[global::app.Attributes.PlangType("gesture"), global::app.Attributes.Choice]
public enum Gesture { move, down, up, wheel }

/// <summary>Which button — none while only moving.</summary>
[global::app.Attributes.PlangType("button"), global::app.Attributes.Choice]
public enum Button { left, right, middle, back, forward, none }

/// <summary>
/// The mouse moved, a button went down or up (<c>clicks</c> 2 for a double click), or the wheel turned
/// (<c>dx</c>/<c>dy</c>) — at (<c>x</c>, <c>y</c>) on the screen, with the modifier keys held.
/// </summary>
public sealed class @this : input.@this
{
    /// <summary>A mouse input, born with the app's <paramref name="types"/>: its own, its choices', and its numbers'
    /// and keys'.</summary>
    public @this(global::app.type.list.@this types, global::app.type.item.input.mouse.Gesture action, int x, int y,
        global::app.type.item.input.mouse.Button button = global::app.type.item.input.mouse.Button.none,
        int clicks = 0, int dx = 0, int dy = 0, global::app.type.item.input.modifier.list.@this? modifier = null,
        long? stamp = null) : base(types[typeof(@this)], stamp)
    {
        Action = new(action, types[typeof(global::app.type.item.choice.@this<global::app.type.item.input.mouse.Gesture>)]);
        Button = new(button, types[typeof(global::app.type.item.choice.@this<global::app.type.item.input.mouse.Button>)]);
        X = global::app.type.item.number.@this.Create(x)!;
        Y = global::app.type.item.number.@this.Create(y)!;
        Clicks = global::app.type.item.number.@this.Create(clicks)!;
        Dx = global::app.type.item.number.@this.Create(dx)!;
        Dy = global::app.type.item.number.@this.Create(dy)!;
        Modifier = modifier ?? new global::app.type.item.input.modifier.list.@this(0, types);
    }

    /// <summary>What it did: move, down, up, wheel.</summary>
    [Out] public global::app.type.item.choice.@this<global::app.type.item.input.mouse.Gesture> Action { get; }
    /// <summary>Where, across the screen.</summary>
    [Out] public global::app.type.item.number.@this X { get; }
    /// <summary>Where, down the screen.</summary>
    [Out] public global::app.type.item.number.@this Y { get; }
    /// <summary>Which button: left, right, middle, back, forward — none while only moving.</summary>
    [Out] public global::app.type.item.choice.@this<global::app.type.item.input.mouse.Button> Button { get; }
    /// <summary>1 for a click, 2 for a double click.</summary>
    [Out] public global::app.type.item.number.@this Clicks { get; }
    /// <summary>How far the wheel turned, sideways.</summary>
    [Out] public global::app.type.item.number.@this Dx { get; }
    /// <summary>How far the wheel turned, down (up is negative).</summary>
    [Out] public global::app.type.item.number.@this Dy { get; }
    /// <summary>The modifier keys held: alt, ctrl, meta, shift.</summary>
    [Out, LlmBuilder] public global::app.type.item.input.modifier.list.@this Modifier { get; }

    private protected override void Applied(ITarget target) => target.Mouse(this);

    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        writer.Name("mouse"); await Action.Output(writer);
        writer.Name("x"); await X.Output(writer);
        writer.Name("y"); await Y.Output(writer);
        if (Action.Value == global::app.type.item.input.mouse.Gesture.wheel)
        {
            writer.Name("dx"); await Dx.Output(writer);
            writer.Name("dy"); await Dy.Output(writer);
        }
        else
        {
            writer.Name("button"); await Button.Output(writer);
            writer.Name("clicks"); await Clicks.Output(writer);
        }
        // the line carries the modifiers held as their code
        writer.Name("mods"); writer.Int(Modifier.Code);
        await WriteStamp(writer);
        writer.EndObject();
    }
}
