namespace app.type.item.input.key;

/// <summary>
/// A key went down or up — by its scancode (<c>sc</c>, the keyboard's own number for the key, whatever the layout),
/// whether it is an extended key (the arrows, Home, the right Ctrl: <c>ext</c>), its virtual key and name when the
/// host knows them, and the modifier keys held (<c>mods</c>). What the key means is the receiving keyboard's to say
/// (its layout): a key is where it is pressed, not what it types.
/// </summary>
public sealed class @this : input.@this
{
    private readonly bool _down, _extended;
    private readonly uint _scancode;
    private readonly int _vk, _mods;
    private readonly string? _name;

    /// <summary>A key by its scancode; its <paramref name="name"/> when given, else the one its virtual key has (none
    /// while Alt is held: those combinations are the system's).</summary>
    public @this(bool down, uint scancode, bool extended = false, int vk = 0, string? name = null, int mods = 0, long? stamp = null)
        : base(stamp)
    {
        _down = down;
        _scancode = scancode;
        _extended = extended;
        _vk = vk;
        _mods = mods;
        _name = name ?? Named(vk, mods);
    }

    // the modifier bits on the wire: Alt 1, Ctrl 2, Shift 4, (Windows' Insert-paste) 8
    private const int Alt = 1, Ctrl = 2;

    // a key's name from its virtual key (Windows' VK codes) — the keys that aren't text, and Ctrl+letter (Ctrl+A, Ctrl+C)
    private static string? Named(int vk, int mods) => (mods & Alt) != 0 ? null : vk switch
    {
        0x0D => "Enter", 0x08 => "Backspace", 0x09 => "Tab", 0x2E => "Delete", 0x1B => "Escape",
        0x24 => "Home", 0x23 => "End", 0x21 => "PageUp", 0x22 => "PageDown",
        0x25 => "ArrowLeft", 0x26 => "ArrowUp", 0x27 => "ArrowRight", 0x28 => "ArrowDown",
        >= 0x41 and <= 0x5A when (mods & Ctrl) != 0 => ((char)('a' + vk - 0x41)).ToString(),
        _ => null,
    };

    /// <summary>True while it goes down, false as it comes up.</summary>
    [Out] public global::app.type.item.@bool.@this Down => _down;
    /// <summary>The keyboard's number for the key.</summary>
    [Out] public global::app.type.item.number.@this Scancode => (long)_scancode;
    /// <summary>An extended key (the arrows, Home, the right Ctrl): its scancode means another key.</summary>
    [Out] public global::app.type.item.@bool.@this Extended => _extended;
    /// <summary>The host's virtual key for it, 0 when not known.</summary>
    [Out] public global::app.type.item.number.@this Vk => _vk;
    /// <summary>The key's name, when it has one (Enter, ArrowLeft, a with Ctrl).</summary>
    [Out] public global::app.type.item.text.@this? Name => _name is { } n ? (global::app.type.item.text.@this)n : null;
    /// <summary>The modifier keys held: alt, ctrl, meta, shift.</summary>
    [Out] public global::app.type.item.list.@this<global::app.type.item.choice.@this<Modifier>> Modifiers => Named(_mods);

    private protected override void Applied(ITarget target) => target.Key(this);

    public override void Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        writer.Name("key"); writer.String(_down ? "down" : "up");
        writer.Name("sc"); writer.Long(_scancode);
        writer.Name("ext"); writer.Bool(_extended);
        writer.Name("vk"); writer.Int(_vk);
        writer.Name("name");
        if (_name is { } name) writer.String(name); else writer.Null();
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
