
using JsonSerializer = System.Text.Json.JsonSerializer;



namespace PLang.Services.Channels.Formatters;

public class ConsoleFormatter : IMessageTypeFormatter
{
    public ConsoleFormatter()
    {
    }

    public object? Format(object content, MessageType type, int statusCode)
    {
        var text = GetAsText(content);
        if (type is MessageType.UserOutput or MessageType.SystemOutput) return text;
        
        var timestamp = DateTime.UtcNow.ToString("o"); // ISO 8601 format
        return $"{timestamp} [{type}]({statusCode}) - {text}";
    }

    private string? GetAsText(object obj)
    {
        if (obj is string text) return text;
        
        string? content = obj.ToString();
        var fullName = obj.GetType().FullName ?? "";
        if (fullName?.IndexOf("[") != -1)
        {
            fullName = fullName.Substring(0, fullName.IndexOf("["));
        }

        return (content?.StartsWith(fullName) ?? false) ? JsonSerializer.Serialize(content) : content?.ToString();
    }
}