using Newtonsoft.Json;

namespace PLang.Services.Channels;

public interface IFormatter
{
    object? Format(object? content);
}

public class TextFormatter : IFormatter
{
    public object? Format(object? obj)
    {
        if (obj == null) return null;

        var content = obj.ToString();
        if (content == null) return null;

        var fullName = obj.GetType().FullName;
        if (fullName?.IndexOf("[") != -1) fullName = fullName?.Substring(0, fullName.IndexOf("["));

        if (fullName != null && content.StartsWith(fullName))
            return JsonConvert.SerializeObject(obj, Formatting.Indented);

        return content;
    }
}