namespace App.Channels;

/// <summary>
/// Thrown when <see cref="@this.Resolve"/> can't locate a channel by name.
/// Source-gen and IChannel resolution surfaces this through PLang's normal
/// error chain (ServiceError with Key="ChannelNotFound") via App.Run's catch.
/// </summary>
public sealed class ChannelNotFoundException : Exception
{
    public string ChannelName { get; }

    public ChannelNotFoundException(string channelName)
        : base($"Channel '{channelName}' not found")
    {
        ChannelName = channelName;
    }
}
