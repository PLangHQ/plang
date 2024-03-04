using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
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
			return await base.Handle(exception);
		}
		public async Task<bool> ShowError(Exception exception, int statusCode, string statusText, string message, GoalStep? step)
		{
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
					if (JsonHelper.IsJson(exception.ToString())) {
						await writer.WriteAsync(exception.ToString());
					}
					else
					{
						await writer.WriteAsync(JsonConvert.SerializeObject(exception.ToString(), Formatting.Indented));

					}
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
