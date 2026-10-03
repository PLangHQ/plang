namespace app.type.item.input.mouse;

/// <summary>What the mouse did: moved, a button down or up, the wheel turned.</summary>
[global::app.Attributes.PlangType("gesture")]
public enum Gesture { move, down, up, wheel }

/// <summary>Which button — none while only moving.</summary>
[global::app.Attributes.PlangType("button")]
public enum Button { left, right, middle, back, forward, none }

/// <summary>
/// The mouse moved, a button went down or up (<c>clicks</c> 2 for a double click), or the wheel turned
/// (<c>dx</c>/<c>dy</c>) — at (<c>x</c>, <c>y</c>) on the screen, with the modifier keys held.
/// </summary>
public sealed class @this : input.@this
{
    // (the enums by their full names: the member Button is this value's plang face)
    private readonly global::app.type.item.input.mouse.Gesture _action;
    private readonly int _x, _y, _clicks, _dx, _dy, _mods;
    private readonly global::app.type.item.input.mouse.Button _button;

    public @this(global::app.type.item.input.mouse.Gesture action, int x, int y,
        global::app.type.item.input.mouse.Button button = global::app.type.item.input.mouse.Button.none,
        int clicks = 0, int dx = 0, int dy = 0, int mods = 0, long? stamp = null) : base(stamp)
    {
        _action = action;
        _x = x;
        _y = y;
        _button = button;
        _clicks = clicks;
        _dx = dx;
        _dy = dy;
        _mods = mods;
    }

    /// <summary>What it did: move, down, up, wheel.</summary>
    [Out] public global::app.type.item.choice.@this<global::app.type.item.input.mouse.Gesture> Action => new(_action);
    /// <summary>Where, across the screen.</summary>
    [Out] public global::app.type.item.number.@this X => _x;
    /// <summary>Where, down the screen.</summary>
    [Out] public global::app.type.item.number.@this Y => _y;
    /// <summary>Which button: left, right, middle, back, forward — none while only moving.</summary>
    [Out] public global::app.type.item.choice.@this<global::app.type.item.input.mouse.Button> Button => new(_button);
    /// <summary>1 for a click, 2 for a double click.</summary>
    [Out] public global::app.type.item.number.@this Clicks => _clicks;
    /// <summary>How far the wheel turned, sideways.</summary>
    [Out] public global::app.type.item.number.@this Dx => _dx;
    /// <summary>How far the wheel turned, down (up is negative).</summary>
    [Out] public global::app.type.item.number.@this Dy => _dy;
    /// <summary>The modifier keys held: alt, ctrl, meta, shift.</summary>
    [Out] public global::app.type.item.list.@this<global::app.type.item.choice.@this<Modifier>> Modifiers => Named(_mods);

    private protected override void Applied(ITarget target) => target.Mouse(this);

    public override void Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        writer.Name("mouse"); writer.String(_action.ToString());
        writer.Name("x"); writer.Int(_x);
        writer.Name("y"); writer.Int(_y);
        if (_action == global::app.type.item.input.mouse.Gesture.wheel)
        {
            writer.Name("dx"); writer.Int(_dx);
            writer.Name("dy"); writer.Int(_dy);
        }
        else
        {
            writer.Name("button"); writer.String(_button.ToString());
            writer.Name("clicks"); writer.Int(_clicks);
        }
        writer.Name("mods"); writer.Int(_mods);
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
