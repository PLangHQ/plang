using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using PLang.Modules.WebCrawlerModule.Models;
using System.Text;

namespace PLang.Modules.WebCrawlerModule
{
	public class WebCrawlerHelper
	{
		public static async Task<Response?> GetResponse(IResponse response)
		{
			string? content = null;
			if (!IsEmptyContent(response))
			{
				try
				{
					await response.FinishedAsync();
					var contentType = (response.Headers.ContainsKey("Content-Type")) ? response.Headers["Content-Type"] : "text/" ?? "text/";

					if (IsText(contentType))
					{
						var bytes = await response.BodyAsync();
						content = Encoding.UTF8.GetString(bytes);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error getting content from {response.Url} - {ex.Message}");
				}
			}

			var headers = response.Headers;
			var parentUrl = response.Frame.Url;
			string? serverIp = null;
			int? serverPort = null;
			ResponseSecurityDetailsResult? securityDetails = null;
			try
			{
				var serverAddress = await response.ServerAddrAsync();
				securityDetails = await response.SecurityDetailsAsync();

				serverIp = serverAddress?.IpAddress;
				serverPort = serverAddress?.Port;
			}
			catch (Exception ex)
			{
				int i = 0;
				Console.WriteLine($"Connection disconnected {response.Url}");

				if (ex.Message.Contains("Connection disposed")) return null;
			}

			return new Response(response.Status, response.StatusText, response.Url, response.Ok, headers, response.FromServiceWorker, parentUrl)
			{
				ServerIp = serverIp,
				ServerPort = serverPort,
				SecurityDetails = securityDetails,
				Content = content

			};

		}

		private static bool IsText(string contentType)
		{
			if (string.IsNullOrEmpty(contentType)) return false;

			// Extended set of known text-based content types
			var textContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
				{
					"text/",
					"application/json",
					"application/xml",
					"application/javascript",
					"application/xhtml+xml",
					"application/ld+json",
					"application/xml+rss",
					"application/atom+xml",
					"application/x-www-form-urlencoded",
					"application/xml+rss",
				};

			// Check if the Content-Type matches any of the known text types
			if (textContentTypes.Contains(contentType))
				return true;

			// If it contains "charset", it is likely a text type (e.g., application/json; charset=utf-8)
			if (contentType.IndexOf("charset=", StringComparison.OrdinalIgnoreCase) >= 0)
				return true;

			return false;
		}

		private static bool IsEmptyContent(IResponse response)
		{
			return (response.Status == 204 || (response.Status >= 300 && response.Status < 400));
		}

		public static Request GetRequest(IRequest request)
		{
			return new Request(request.Failure, request.Url, request.Method, request.RedirectedFrom?.Url, request.Headers, request.IsNavigationRequest, request.RedirectedTo?.Url, request.ResourceType, request.PostData)
			{

			};
		}
	}
}
