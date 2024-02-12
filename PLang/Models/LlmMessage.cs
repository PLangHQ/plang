using Newtonsoft.Json;

namespace PLang.Models
{
	public class LlmMessage
	{
		public LlmMessage() { }
		public LlmMessage(string role, List<LlmContent> content)
		{
			if (role != "system" && role != "assistant" && role != "user")
			{
				throw new Exception($"role '{role}' is not valid. Only system, assistant, user is valid");
			}

			this.Role = role;
			this.Content = content;
		}
		public LlmMessage(string role, string content)
		{
			this.Role = role;
			this.Content = new List<LlmContent>() { new LlmContent(content) };
		}
		[JsonProperty("role")]
		public string Role { get; set; }
		[JsonProperty("content")]
		public List<LlmContent> Content { get; set; }
	}
	public class LlmContent
	{
		public LlmContent(string text, string type = "text", ImageUrl? imageUrl = null)
		{
			this.Text = text;
			this.Type = type;
			this.ImageUrl = imageUrl;
		}
		[JsonProperty("type")]
		public string Type = "text";
		[JsonProperty("text")]
		public string Text { get; set; }
		[JsonProperty("image_url")]
		public ImageUrl ImageUrl { get; set; }
	}

	public class ImageUrl
	{
		public ImageUrl(string url) {
			this.Url = url;
		}
		[JsonProperty("url")]
		public string Url { get; set; }
	}
}
