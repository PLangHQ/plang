using Newtonsoft.Json;

namespace PLang.Models;

public class LlmMessage
{
    public LlmMessage()
    {
    }

    public LlmMessage(string role, List<LlmContent> content)
    {
        if (role != "system" && role != "assistant" && role != "user")
            throw new Exception($"role '{role}' is not valid. Only system, assistant, user is valid");

        Role = role;
        Content = content;
    }

    public LlmMessage(string role, string content)
    {
        Role = role;
        Content = new List<LlmContent> { new(content) };
    }

    [JsonProperty("role")] public string Role { get; set; }

    [JsonProperty("content")] public List<LlmContent> Content { get; set; }
}

public class LlmContent
{
    [JsonProperty("type")] public string Type = "text";

    public LlmContent(string text, string type = "text", ImageUrl? imageUrl = null)
    {
        Text = text;
        Type = type;
        ImageUrl = imageUrl;
    }

    [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
    public string Text { get; set; }

    [JsonProperty("image_url", NullValueHandling = NullValueHandling.Ignore)]
    public ImageUrl ImageUrl { get; set; }
}

public class ImageUrl
{
    public ImageUrl(string url)
    {
        Url = url;
    }

    [JsonProperty("url")] public string Url { get; set; }
}