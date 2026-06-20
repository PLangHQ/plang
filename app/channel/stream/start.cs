using app.channel.config;
using app.channel.format;

namespace app.channel.stream;

// A stream is the byte boundary — the raw sink/source. It is the only place Data
// becomes bytes. Above the stream everything is Data; below it everything is
// bytes. The format that bridges the two follows the channel's mime: the stream
// asks the format registry for the format that handles config.mime. Change the
// mime → the format changes, with no code touched here.
//
// The stream is the leaf: it reads its own config (the mime) and opens the
// payload only here, at the boundary. No method carries Async in its name — the
// only WriteAsync in sight is the underlying .NET Stream's, called in WriteRaw.
abstract class stream(format.list format, data.@this<config.config> config) {
    public data.@this<config.config> config { get; } = config;

    // Just before the bytes leave: resolve the format from the mime, serialize
    // the Data, then write raw.
    public async Task<data.@this> Write(data.@this data) {
        var cfg    = await config.Value();              // leaf reads its own config
        var format = await this.format.@for(cfg.mime);  // format follows the mime
        var bytes  = format.serialize(data);
        return await WriteRaw(bytes);
    }

    // Read raw, then the format makes Data of it — stamped with the mime's type.
    public async Task<data.@this> Read() {
        var cfg    = await config.Value();
        var format = await this.format.@for(cfg.mime);
        return format.deserialize(await ReadRaw(), cfg.mime);
    }

    // The actual byte I/O — each concrete stream (file, console, memory, http)
    // implements only these, and only here does a .NET Stream.WriteAsync /
    // ReadAsync get called. Serialization is already done; this just moves bytes.
    protected abstract Task<data.@this> WriteRaw(data.@this<type.binary.@this> bytes);
    protected abstract Task<data.@this<type.binary.@this>> ReadRaw();
}
