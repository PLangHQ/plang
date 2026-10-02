using app.Attributes;
using app.error;

namespace app.module.screen.type.screen;

/// <summary>
/// PLang <c>screen</c> value — where pictures are shown, opened with <c>screen.open</c>. Two kinds, each owning what it
/// does: a <see cref="window.@this"/> on the host (Windows) that shows the frames drawn into it and gives out its
/// mouse and keyboard (OnInput), and PlangOS's <see cref="display.@this"/> (Linux) that programs draw onto, which takes
/// the host's input (screen.send, screen.listen) and sends its frames up. A goal asks <c>if %screen% is screen</c>, and
/// which it is by its kind: <c>%screen!type.kind%</c> (<c>window</c>, <c>display</c>).
/// </summary>
[PlangType("screen"), Kinds]
public abstract class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    private readonly string _title;
    private readonly int _width, _height;

    private protected @this(string title, int width, int height)
    {
        _title = title;
        _width = width;
        _height = height;
    }

    /// <summary>Its kind — window, display.</summary>
    private protected abstract string Variant { get; }

    protected internal override global::app.type.@this Type => new("screen", typeof(@this), Variant);

    public override bool IsLeaf => false;

    /// <summary>The screen's title.</summary>
    [LlmBuilder, Out] public global::app.type.item.text.@this Title => _title;

    /// <summary>Width of the drawing area in pixels.</summary>
    [LlmBuilder, Out] public global::app.type.item.number.@this Width => _width;

    /// <summary>Height of the drawing area in pixels.</summary>
    [LlmBuilder, Out] public global::app.type.item.number.@this Height => _height;

    /// <summary>True once it is closed.</summary>
    [LlmBuilder, Out] public global::app.type.item.@bool.@this Closed => IsClosed;

    /// <summary>Frames drawn so far.</summary>
    [LlmBuilder, Out] public global::app.type.item.number.@this Frames => Drawn;

    /// <summary>Whether the screen says what happens to its windows as it happens — opened, the size each is asked to
    /// be, its first picture, shown, minimized, focused, closed: <c>set %screen.verbose% to true</c>. On under
    /// <c>--debug</c>. The notes go to the debug output (<c>--debug</c>), else the error output — never to a goal.</summary>
    [LlmBuilder, Out]
    public global::app.type.item.@bool.@this Verbose
    {
        get => Noting;
        set => Noting = value.Value;
    }

    /// <summary>Its pixel size, for what draws onto it.</summary>
    internal int PixelWidth => _width;
    internal int PixelHeight => _height;

    private protected abstract bool IsClosed { get; }
    private protected virtual int Drawn => 0;
    private protected virtual bool Noting { get => false; set { } }

    private Elements? _element;
    /// <summary>Its elements, picked by selector (<c>%!screen.element["#window.bot"]%</c>) — to bind on their events.</summary>
    public Elements element => _element ??= new(this);

    /// <summary>One step by dot: <c>element</c> — its elements; any other member as every item's.</summary>
    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
        => string.Equals(key, "element", StringComparison.OrdinalIgnoreCase)
            ? System.Threading.Tasks.ValueTask.FromResult(new global::app.data.@this(key, element, parent: parent))
            : base.Get(parent, key);

    /// <summary>Draws <paramref name="frame"/> (a frame line, or an image): <c>screen.draw</c>.</summary>
    internal abstract Task<global::app.data.@this> Draw(global::app.data.@this frame, global::app.actor.context.@this context);

    /// <summary>Gives it one line from the host — an input, a command for one of its windows: <c>screen.send</c>.</summary>
    internal abstract Task<global::app.data.@this> Send(global::app.data.@this line, global::app.actor.context.@this context);

    /// <summary>Takes this app's input itself, line by line, until it ends: <c>screen.listen</c>.</summary>
    internal virtual Task<global::app.data.@this> Listen(global::app.actor.context.@this context)
        => Task.FromResult(context.Error(new ActionError("This screen takes no input (it isn't PlangOS's display).", "NotSupported", 400)));

    /// <summary>A message from PlangOS's screen, straight to it (no goal per frame): what
    /// <c>terminal.open … binary output to %screen%</c> does with each one. False when it takes none.</summary>
    internal virtual bool Show(byte[] message) => false;

    /// <summary>An element's events were reached: its clicks are its own from now on.</summary>
    internal virtual void Bind(string selector) { }

    /// <summary>Closes it: <c>screen.close</c>.</summary>
    internal abstract void Close();

    public override string ToString() => $"screen '{_title}' {_width}x{_height}{(IsClosed ? ", closed" : "")}";
}
