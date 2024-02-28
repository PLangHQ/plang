using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Utils;
using System.Net;
using System.Text;

namespace PLang.Exceptions.Handlers
{
	public class HttpExceptionHandler : ExceptionHandler, IExceptionHandler
	{
		private readonly HttpListenerContext httpListenerContext;
		private readonly ILogger logger;

		public HttpExceptionHandler(HttpListenerContext httpListenerContext, IAskUserHandlerFactory askUserHandlerFactory, ILogger logger) : base(askUserHandlerFactory)
		{
			this.httpListenerContext = httpListenerContext;
			this.logger = logger;
		}

		public async Task<bool> Handle(Exception exception, int statusCode, string statusText, string message)
		{
			if (await base.Handle(exception)) { return true; }

			AppContext.TryGetSwitch(ReservedKeywords.Debug, out bool isDebug);

			var response = new Dictionary<string, object>();
			response.Add(statusText, message);
			if (isDebug)
			{
				response.Add("exception", exception);
			}

			try
			{
				var resp = httpListenerContext.Response;
				
				resp.StatusCode = statusCode;
				resp.StatusDescription = statusText;

				using (var writer = new StreamWriter(resp.OutputStream, resp.ContentEncoding ?? Encoding.UTF8))
				{
					await writer.WriteAsync(exception.ToString());
					await writer.FlushAsync();
				}


				return false;
			}
			catch (ObjectDisposedException)
			{
				return false;
			}
			catch (Exception ex)
			{
				logger.LogError(@$"Two exception happen. 

The original exception: 
Exception:{exception}
Response: {JsonConvert.SerializeObject(response)}
The exception in HttpExceptionHandler: {ex}
");
				return false;
			}
		}
	}
}
