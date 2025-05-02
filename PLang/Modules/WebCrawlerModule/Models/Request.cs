using Microsoft.Playwright;
using System.Text.Json.Serialization;

namespace PLang.Modules.WebCrawlerModule.Models
{
	public record Request(string? Failure, string Url, string Method, string? RedirectedFrom, Dictionary<string, string> Headers, 
		bool IsNavigationRequest, string? RedirectedTo, string ResourceType, string? PostData)
	{

	}
}
