namespace PLang.Tests.Shared;

/// <summary>What the user actor's output channel receives — a stream channel registered in its place, so a goal
/// runs through the real <c>output.write</c> and the test reads what it wrote.</summary>
public sealed class CapturedOutput
{
    private readonly System.IO.MemoryStream _stream = new();

    public CapturedOutput(global::app.@this app)
    {
        app.actor.list.User.Channel.Register(new global::app.channel.type.stream.@this(
            global::app.channel.list.@this.Output, _stream,
            global::app.channel.ChannelDirection.Output, ownsStream: false) { Mime = "text/plain" });
    }

    /// <summary>Everything written so far.</summary>
    public string Text => System.Text.Encoding.UTF8.GetString(_stream.ToArray());
}
