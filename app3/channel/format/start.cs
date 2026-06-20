using app.channel.mime;

namespace app.channel.format;

// A format is a serializer for one shape of bytes: text, json, the plang wire.
// It owns both directions for the mimes it handles. serialize turns Data into
// bytes on the way out; deserialize makes Data of the bytes on the way in,
// stamping the value with the mime's plang type. Nothing above the stream knows
// which format is in play — the stream picks it from the mime.
abstract class format {
    // Does this format own this content type? json handles application/json,
    // text handles text/*, the plang format handles application/plang.
    public abstract bool handles(mime.mime mime);

    public abstract data.@this<type.binary.@this> serialize(data.@this data);
    public abstract data.@this deserialize(data.@this<type.binary.@this> bytes, mime.mime mime);
}

// format.list — the formats a stream can speak. To change a channel's serializer
// you change its mime; the registry hands back the format that handles it. To add
// a new serializer, register a format here — no channel or stream changes.
class list(data.@this<plang.list<format>> formats) {
    public data.@this<plang.list<format>> list => formats;
    public async Task<format> @for(mime.mime mime) =>
        (await formats.Value()).first(f => f.handles(mime));
}
