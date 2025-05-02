using Microsoft.Playwright;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace PLang.Modules.WebCrawlerModule.Models
{


	public record Response(int Status, string StatusText, string Url, bool Ok, Dictionary<string, string> Headers,
		bool FromServiceWorker, string ParentUrl)
	{
		public bool IsJavascript => Url.EndsWith(".js") || ContentType.Contains("javascript");
		public bool IsCss => Url.EndsWith(".css") || ContentType.Contains("text/css");
		public bool IsHtml => Url.EndsWith(".html") || ContentType.Contains("text/html");
		public bool IsJson => ContentType.Contains("application/json");
		public bool IsImage => ContentType.StartsWith("image/");
		public bool IsVideo => ContentType.StartsWith("video/");
		public bool IsPdf => ContentType.StartsWith("/pdf");
		public bool IsAudio => ContentType.StartsWith("audio/");
		public bool IsFont => ContentType.StartsWith("font/") || ContentType.Contains("woff");
		
		public string ContentType => Headers.TryGetValue("content-type", out var ct) ? ct : "";
		public bool IsContentType(string type) => ContentType.Contains(type);
		public string? Encoding
		{
			get
			{
				if (string.IsNullOrEmpty(ContentType)) return null;

				string searchFor = "charset=";
				var idx = ContentType.IndexOf(searchFor);
				if (idx == -1) return null;

				var encoding = ContentType.Substring(idx + searchFor.Length);
				return encoding;
			}
		}
		public string? ServerIp { get; set; }
		public int? ServerPort { get; set; }
		public ResponseSecurityDetailsResult? SecurityDetails { get; set; }
		

		public string? Content { get; set; }

	}
}
