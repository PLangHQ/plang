namespace app.type.item.input.key;

/// <summary>
/// A key went down or up — by its scancode (<c>sc</c>, the keyboard's own number for the key, whatever the layout),
/// whether it is an extended key (the arrows, Home, the right Ctrl: <c>ext</c>), its virtual key and name when the
/// host knows them, and the modifier keys held (<c>mods</c>). What the key means is the receiving keyboard's to say
/// (its layout): a key is where it is pressed, not what it types.
/// </summary>
public sealed class @this : input.@this
{
    /// <summary>A key by its scancode, born with the app's <paramref name="types"/>; its <paramref name="name"/> when
    /// given, else the one its virtual key has (none while Alt is held: those combinations are the system's).</summary>
    public @this(global::app.type.list.@this types, bool down, uint scancode, bool extended = false, int vk = 0,
        string? name = null, global::app.type.item.input.modifier.list.@this? modifier = null, long? stamp = null)
        : base(types[typeof(@this)], stamp)
    {
        Down = new global::app.type.item.@bool.@this(down, types["bool"]);
        Scancode = global::app.type.item.number.@this.Create((long)scancode)!;
        Extended = new global::app.type.item.@bool.@this(extended, types["bool"]);
        Vk = global::app.type.item.number.@this.Create(vk)!;
        Modifier = modifier ?? new global::app.type.item.input.modifier.list.@this(0, types);
        Name = (name ?? Named(vk, Modifier)) is { } named ? new global::app.type.item.text.@this(named, types["text"]) : null;
    }

    // a key's name from its virtual key (Windows' VK codes) — the keys that aren't text, and Ctrl+letter (Ctrl+A, Ctrl+C)
    private static string? Named(int vk, global::app.type.item.input.modifier.list.@this modifier)
        => modifier.Holds("alt") ? null : vk switch
        {
            0x0D => "Enter", 0x08 => "Backspace", 0x09 => "Tab", 0x2E => "Delete", 0x1B => "Escape",
            0x24 => "Home", 0x23 => "End", 0x21 => "PageUp", 0x22 => "PageDown",
            0x25 => "ArrowLeft", 0x26 => "ArrowUp", 0x27 => "ArrowRight", 0x28 => "ArrowDown",
            >= 0x41 and <= 0x5A when modifier.Holds("ctrl") => ((char)('a' + vk - 0x41)).ToString(),
            _ => null,
        };

    /// <summary>True while it goes down, false as it comes up.</summary>
    [Out] public global::app.type.item.@bool.@this Down { get; }
    /// <summary>The keyboard's number for the key.</summary>
    [Out] public global::app.type.item.number.@this Scancode { get; }
    /// <summary>An extended key (the arrows, Home, the right Ctrl): its scancode means another key.</summary>
    [Out] public global::app.type.item.@bool.@this Extended { get; }
    /// <summary>The host's virtual key for it, 0 when not known.</summary>
    [Out] public global::app.type.item.number.@this Vk { get; }
    /// <summary>The key's name, when it has one (Enter, ArrowLeft, a with Ctrl).</summary>
    [Out] public global::app.type.item.text.@this? Name { get; }
    /// <summary>The modifier keys held: alt, ctrl, meta, shift.</summary>
    [Out] public global::app.type.item.input.modifier.list.@this Modifier { get; }

    private protected override void Applied(ITarget target) => target.Key(this);

    public override void Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        writer.Name("key"); writer.String(Down.Value ? "down" : "up");
        writer.Name("sc"); Scancode.Write(writer);
        writer.Name("ext"); Extended.Write(writer);
        writer.Name("vk"); Vk.Write(writer);
        writer.Name("name");
        if (Name is { } name) name.Write(writer); else writer.Null();
        writer.Name("mods"); Modifier.Write(writer);
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
