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

    public @this(bool down, uint scancode, bool extended = false, int vk = 0, string? name = null, int mods = 0, long? stamp = null)
        : base(stamp)
    {
        _down = down;
        _scancode = scancode;
        _extended = extended;
        _vk = vk;
        _name = name;
        _mods = mods;
    }

    private protected override string Variant => "key";

    /// <summary>down or up.</summary>
    [Out] public global::app.type.item.text.@this Action => _down ? "down" : "up";
    /// <summary>The keyboard's number for the key.</summary>
    [Out] public global::app.type.item.number.@this Scancode => (long)_scancode;
    /// <summary>The key's name, when the host knows it (Enter, F5, A).</summary>
    [Out] public global::app.type.item.text.@this? Name => _name is { } n ? n : null;

    private protected override void Applied(ITarget target) => target.Key(_down, _scancode, _extended, _vk, _name, _mods);

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
