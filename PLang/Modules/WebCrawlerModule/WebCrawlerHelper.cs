using Microsoft.Playwright;
using PLang.Modules.WebCrawlerModule.Models;

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
					content = await response.TextAsync();
				}
				catch (Exception ex)
				{
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
			} catch (Exception ex)
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
