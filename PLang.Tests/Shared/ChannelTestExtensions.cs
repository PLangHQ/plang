namespace PLang.Tests.Shared;

/// <summary>Test conveniences on an actor's channels.</summary>
public static class ChannelTestExtensions
{
    /// <summary>Creates an in-memory channel under <paramref name="name"/> and registers it.</summary>
    public static global::app.channel.@this CreateMemoryChannel(this global::app.channel.list.@this channels, string name,
        global::app.channel.ChannelDirection direction = global::app.channel.ChannelDirection.Bidirectional)
    {
        var channel = global::app.channel.type.stream.@this.Memory(name, direction);
        channels.Register(channel);
        return channel;
    }
}
