namespace PLang.Services.Channels.Formatters;

public interface IMessageTypeFormatter
{
    object? Format(object content, MessageType type, int statusCode);
}