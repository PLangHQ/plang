using app.type.text;

namespace app.channel;

// Implement IChannel to BE a channel. The one thing that differs between
// channels is the stream — what you write to: a file, the console, an http
// response body, a goal. Supply that and you get Write / Read for free.
interface IChannel {
    stream.stream stream { get; }
}

// A channel is named I/O for an actor. It holds its config and forwards to the
// stream. The stream is NOT handed in — it comes from implementing IChannel,
// because it depends on what this channel writes to. The channel never
// serializes, never sees bytes; that lives on the stream, the byte boundary.
abstract class channel(data.@this<text.@this> name, data.@this<config.config> config) : IChannel {
    public data.@this<text.@this>        name   { get; } = name;
    public data.@this<config.config>     config { get; } = config;

    // Provided by the concrete channel — file → file stream, console → console
    // stream, http → response-body stream. This is the IChannel surface.
    public abstract stream.stream stream { get; }

    public bool is(data.@this<text.@this> target) => name.Equals(target);

    // channel.write => stream.write. Forward; the stream serializes, writes, and
    // refuses the wrong direction — it holds the config.
    public Task<data.@this> Write(data.@this data) => stream.Write(data);
    public Task<data.@this> Read()                 => stream.Read();
}

// channel.list — the channels of an actor. The list owns lookup; the channel
// answers to its own name. The three defaults (output, error, input) are
// registered here at boot and cannot be removed.
class list(data.@this<plang.list<channel>> channels) {
    public data.@this<plang.list<channel>> list => channels;

    // The list owns its channels — reading its own backing to search is owner
    // behavior, not decomposition. The channel answers to its own name.
    public async Task<channel?> find(data.@this<text.@this> name) =>
        (await channels.Value()).first(c => c.is(name));
}
