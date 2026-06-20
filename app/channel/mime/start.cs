using app.type.text;

namespace app.channel.mime;

// A mime is the content type of a channel — "text/plain", "application/json",
// "image/png". It is the single knob that answers both of the channel's hard
// questions:
//
//   - which serializer?  → the format whose `handles(mime)` is true
//   - which plang type?  → `mime.type` — text/plain → text, application/json →
//                          {object, json}, image/png → {image, png}
//
// So the mime is not just a string the channel carries — it is the thing that
// maps the wire to the value. Change the mime and the serializer and the
// read-type both change with it; nothing else has to.
class mime(data.@this<text.@this> name) {
    public data.@this<text.@this> name { get; } = name;

    // The plang type this content type resolves to — the type stamped on reads.
    // The mapping lives in the type system (mime ↔ type); the mime asks for it.
    public type.@this type => app.type.from(this);

    public bool is(data.@this<text.@this> target) => name.Equals(target);
}
