namespace app.type.item.input.serializer;

/// <summary>
/// Typed pull reader for <see cref="app.type.item.input.@this"/> — the read-back of each variant's own Write: the
/// object's first name says which (<c>mouse</c>, <c>key</c>, <c>text</c>, <c>nav</c>), its other members fill it, and
/// members it doesn't know are skipped. An object naming no input is refused, never guessed.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("input", kind);
        reader.BeginObject();
        string? variant = null, what = null, button = null, name = null;
        int x = 0, y = 0, clicks = 0, dx = 0, dy = 0, mods = 0, vk = 0;
        long scancode = 0;
        long? stamp = null;
        bool extended = false;
        while (reader.NextName(out var member))
        {
            switch (member)
            {
                case "mouse" or "key" or "text" or "nav" when variant == null:
                    variant = member;
                    what = reader.String();
                    break;
                case "x": x = reader.Int(); break;
                case "y": y = reader.Int(); break;
                case "button": button = reader.String(); break;
                case "clicks": clicks = reader.Int(); break;
                case "dx": dx = reader.Int(); break;
                case "dy": dy = reader.Int(); break;
                case "mods": mods = reader.Int(); break;
                case "sc": scancode = reader.Long(); break;
                case "ext": extended = reader.Bool(); break;
                case "vk": vk = reader.Int(); break;
                case "name": name = reader.Null() ? null : reader.String(); break;
                case "t": stamp = reader.Long(); break;
                default: reader.Skip(); break;
            }
        }
        reader.EndObject();
        return variant switch
        {
            "mouse" => new mouse.@this(Parsed<mouse.Gesture>(what, "mouse"), x, y,
                button == null ? mouse.Button.none : Parsed<mouse.Button>(button, "button"), clicks, dx, dy, mods, stamp),
            "key" => new key.@this(what == "down" ? true : what == "up" ? false
                : throw new FormatException($"a key goes down or up, not '{what}'"), (uint)scancode, extended, vk, name, mods, stamp),
            "text" => new text.@this(what ?? "", stamp),
            "nav" => new navigate.@this(Parsed<navigate.Direction>(what, "nav"), stamp),
            _ => throw new FormatException("an input names what it is first: mouse, key, text or nav"),
        };
    }

    // a value from its closed set, or why it isn't one — a malformed input is an error, never a guess
    private static T Parsed<T>(string? value, string member) where T : struct, Enum
        => Enum.TryParse<T>(value, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new FormatException($"{member} is one of {string.Join(", ", Enum.GetNames<T>())}, not '{value}'");
}
