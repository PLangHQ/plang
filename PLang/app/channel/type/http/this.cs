using app.error;

namespace app.channel.type.http;

/// <summary>
/// HTTP channel kind — bidirectional: a request is written, a response is read.
/// http is not a peer of channel, it <em>is</em> a channel: the response body
/// enters through the one boundary (<see cref="global::app.channel.@this.StampReadAsync"/>),
/// stamped <c>{type, kind}</c> from the response <c>Content-Type</c> and held as
/// <em>lazy</em> Data — the body parses only when touched, never at read time.
///
/// <para>The transport (signing, size limits, the plang-wire branch) lives in
/// <c>module/http/code</c>; this kind owns the response→lazy-Data stamping. The
/// response metadata (status, headers, duration) rides as Data <c>Properties</c>
/// (read with <c>!</c>), so a status check never materializes the body. The
/// parallel <c>http.response</c> record is gone — the result is plain Data.</para>
/// </summary>
public sealed class @this : global::app.channel.@this
{
    private readonly byte[] _body;

    /// <summary>
    /// Wraps an already-fetched response body for stamping. <paramref name="contentType"/>
    /// drives the <c>{type, kind}</c> stamp; <paramref name="body"/> is the raw
    /// payload held verbatim until touched.
    /// </summary>
    public @this(string contentType, byte[] body, global::app.actor.context.@this? context)
    {
        Name = "http";
        Direction = ChannelDirection.Bidirectional;
        // A response with no Content-Type is read as text — web content is text by
        // default; raw bytes are the exception (the server names them explicitly).
        Mime = string.IsNullOrEmpty(contentType) ? "text/plain" : contentType;
        _body = body;
        if (context != null)
        {
            Actor = context.Actor;
            Channels = context.Actor.Channel;
        }
    }

    public override Task<global::app.data.@this> Read(CancellationToken ct = default)
        => Read(_body, ct);

    public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
        => Task.FromResult(data.Context.Error(new ServiceError(
            "http channel write (request send) flows through the http module transport", "ChannelWriteUnsupported", 400)));

    public override Task<global::app.data.@this> Ask(module.output.ask action, CancellationToken ct = default)
        => Task.FromResult(action.Context.Error(new ServiceError(
            "http channel does not support ask", "ChannelNoAsk", 400)));
}
