namespace app.type.item.input.mouse;

/// <summary>What the mouse did.</summary>
public enum Action { move, down, up, wheel }

/// <summary>Which button — none while only moving.</summary>
public enum Button { left, right, middle, back, forward, none }

/// <summary>
/// The mouse moved, a button went down or up (<c>clicks</c> 2 for a double click), or the wheel turned
/// (<c>dx</c>/<c>dy</c>) — at (<c>x</c>, <c>y</c>) on the screen, with the modifier keys held (<c>mods</c>).
/// </summary>
public sealed class @this : input.@this
{
    // (the enums by their full names: the members Action and Button are this value's plang faces)
    private readonly global::app.type.item.input.mouse.Action _action;
    private readonly int _x, _y, _clicks, _dx, _dy, _mods;
    private readonly global::app.type.item.input.mouse.Button _button;

    public @this(global::app.type.item.input.mouse.Action action, int x, int y,
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

    private protected override string Variant => "mouse";

    /// <summary>What it did: move, down, up, wheel.</summary>
    [Out] public global::app.type.item.text.@this Action => _action.ToString();
    /// <summary>Where, across the screen.</summary>
    [Out] public global::app.type.item.number.@this X => _x;
    /// <summary>Where, down the screen.</summary>
    [Out] public global::app.type.item.number.@this Y => _y;
    /// <summary>Which button: left, right, middle, back, forward — none while only moving.</summary>
    [Out] public global::app.type.item.text.@this Button => _button.ToString();
    /// <summary>1 for a click, 2 for a double click.</summary>
    [Out] public global::app.type.item.number.@this Clicks => _clicks;

    private protected override void Applied(ITarget target) => target.Mouse(_action, _x, _y, _button, _clicks, _dx, _dy, _mods);

    public override void Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        writer.Name("mouse"); writer.String(_action.ToString());
        writer.Name("x"); writer.Int(_x);
        writer.Name("y"); writer.Int(_y);
        if (_action == global::app.type.item.input.mouse.Action.wheel)
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
