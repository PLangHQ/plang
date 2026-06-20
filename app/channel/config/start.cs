using app.type.text;
using app.type.number;
using app.type.duration;

namespace app.channel.config;

// The settled knobs of a channel — its state, not data in flight. So these are
// plang value types, not Data<T>: by the time they're config, the channel.set
// action (the leaf) has already read the incoming Data and stored the value.
//
// mime is the keystone. It names the content type, and from it follow two
// things: the format (serializer) the stream uses, and the plang type stamped on
// every read. Change mime → both follow. Nothing else needs to change.
class config(
    direction              direction,    // out | in | both
    mime.mime              mime,         // text/plain, application/json — selects format + read-type
    text.@this             encoding,     // utf-8
    number.@this           buffer,       // byte buffer size
    duration.@this         timeout,      // i/o timeout
    text.@this?            signing,      // signing provider; "auto" → system identity at write
    text.@this?            encryption)   // encryption provider; none by default
{
    public direction      direction  { get; } = direction;
    public mime.mime      mime       { get; } = mime;
    public text.@this     encoding   { get; } = encoding;
    public number.@this   buffer     { get; } = buffer;
    public duration.@this timeout    { get; } = timeout;
    public text.@this?    signing    { get; } = signing;
    public text.@this?    encryption { get; } = encryption;
}

// What a channel allows. The default console pair is split on purpose:
// output is write-only, input is read-only — so you cannot read stdout or
// write stdin. A channel refuses the wrong direction with a typed error,
// it does not silently no-op.
abstract class direction {
    public abstract bool canWrite { get; }
    public abstract bool canRead  { get; }

    public Task<data.@this> refuseWrite(data.@this<text.@this> name) =>
        Task.FromResult(error.fail(name, "is read-only"));
    public Task<data.@this> refuseRead(data.@this<text.@this> name) =>
        Task.FromResult(error.fail(name, "is write-only"));
}

class @out  : direction { public override bool canWrite => true;  public override bool canRead => false; }
class @in   : direction { public override bool canWrite => false; public override bool canRead => true;  }
class both  : direction { public override bool canWrite => true;  public override bool canRead => true;  }
