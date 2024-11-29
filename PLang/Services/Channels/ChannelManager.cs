using PLang.Errors;

namespace PLang.Services.Channels;

public class ChannelManager
{
    private Dictionary<MessageType, IChannel> channels;

    public ChannelManager(Dictionary<MessageType, IChannel> channels)
    {
        this.channels = channels;
    }
    
    public (IChannel? Channel, IError? Error) GetChannel(MessageType messageType)
    {
        this.channels.TryGetValue(messageType, out IChannel? channel);
        if (channel == null) return (null, new Error($"Channel for {messageType} not found"));
        
        channel.MessageType = messageType;
        return (channel, null);
    }
}