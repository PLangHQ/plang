using Data = global::app.data.@this;

namespace app.type.item.input;

/// <summary>
/// PLang <c>input</c> value — something a person did on a screen: the mouse moved or was clicked or wheeled
/// (<see cref="mouse.@this"/>), a key went down or up (<see cref="key.@this"/>), text was typed
/// (<see cref="text.@this"/>), back, forward or reload was asked (<see cref="navigate.@this"/>). Each variant is a
/// value of its own data, reporting its type as <c>input</c> with the variant as the kind — so a goal asks
/// <c>if %event% is input</c>, or <c>is mouse</c>. What takes input (a display, a window) is handed each by its own
/// <see cref="Apply"/>, never by asking a message's keys.
///
/// <para>On the wire it writes itself as the screen's input line has always been — <c>{"mouse": "down", "x": 4, …}</c>,
/// <c>{"key": "down", "sc": 30, …}</c>, <c>{"text": "a"}</c>, <c>{"nav": "back"}</c> — and <c>serializer/Reader.cs</c>
/// reads that back by its first name. That shape is a step on the way: once the screen's own message type carries
/// plang's Data, an input goes as itself.</para>
/// </summary>
[global::app.Attributes.PlangType("input"), global::app.Attributes.Kinds]
public abstract class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "{\"mouse\": \"down\", \"x\": 120, \"y\": 40, \"button\": \"left\", \"clicks\": 1}";
    public static string Description => "Something a person did on a screen: the mouse moved, clicked or wheeled, a key went down or up, text was typed, or back, forward or reload was asked.";
    public static string Shape => "object";

    // the host's clock when it happened ("t"): echoed with the picture that answers it, so the time from a click to
    // its picture can be measured. Absent on input that needs no answer timed.
    private readonly long? _stamp;

    private protected @this(long? stamp) => _stamp = stamp;

    /// <summary>Its variant — mouse, key, text, navigate: the kind of input.</summary>
    private protected abstract string Variant { get; }

    /// <summary>An input's type is <c>input</c>; its variant is the kind — <c>is input</c> and <c>is mouse</c> both
    /// answer.</summary>
    protected internal override global::app.type.@this Type => new("input", typeof(@this), Variant);

    public override bool IsLeaf => false;

    /// <summary>The host's clock when it happened, when it carries one.</summary>
    [Out] public global::app.type.item.number.@this? Stamp => _stamp is { } t ? (global::app.type.item.number.@this)t : null;

    /// <summary>Hands itself to what takes input: the stamp first (the answer's picture echoes it), then the variant
    /// to the target's own door for it.</summary>
    public void Apply(ITarget target)
    {
        if (_stamp is { } t) target.Stamped(t);
        Applied(target);
    }

    private protected abstract void Applied(ITarget target);

    /// <summary>Its members, then the stamp, inside the object the variant opens.</summary>
    private protected void WriteStamp(global::app.type.format.IWriter writer)
    {
        if (_stamp is not { } t) return;
        writer.Name("t");
        writer.Long(t);
    }

    /// <summary>An input passes; anything else declines — an input is made by what saw it happen (a screen), or read
    /// back from its line.</summary>
    public static @this? Create(object? raw) => raw as @this;

    public static @this? Create(object? raw, global::app.type.@this? declared, Data data)
    {
        if (raw is @this input) return input;
        data.Fail(new global::app.error.Error("an input is made by a screen (a mouse, a key, typed text, back/forward/reload), or read from its line", "InputInvalid", 400));
        return null;
    }
}

/// <summary>What takes input — a display, a window: one door per variant, each handed the variant's own values.</summary>
public interface ITarget
{
    /// <summary>The host's clock for the input that follows, to echo with the picture that answers it.</summary>
    void Stamped(long stamp);

    /// <summary>The mouse: <paramref name="action"/> (move, down, up, wheel) at (<paramref name="x"/>, <paramref name="y"/>),
    /// <paramref name="button"/> (left, right, middle, back, forward, none) <paramref name="clicks"/> times, the wheel's
    /// <paramref name="dx"/>/<paramref name="dy"/>, the modifier keys held.</summary>
    void Mouse(mouse.Action action, int x, int y, mouse.Button button, int clicks, int dx, int dy, int mods);

    /// <summary>A key down or up: its scancode, whether it is an extended key, its virtual key and name when known,
    /// the modifier keys held.</summary>
    void Key(bool down, uint scancode, bool extended, int vk, string? name, int mods);

    /// <summary>Text typed — a character or what an input method composed.</summary>
    void Text(string typed);

    /// <summary>Back, forward or reload, asked by a key or a mouse button.</summary>
    void Navigate(navigate.Direction to);
}
