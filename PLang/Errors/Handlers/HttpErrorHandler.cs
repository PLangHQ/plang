using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Utils;
using System.Net;
using System.Text;

namespace PLang.Errors.Handlers
{
	public class HttpErrorHandler : BaseErrorHandler, IErrorHandler
	{
		private readonly HttpListenerContext httpListenerContext;
		private readonly ILogger logger;

		public HttpErrorHandler(HttpListenerContext httpListenerContext, IAskUserHandlerFactory askUserHandlerFactory, ILogger logger) : base(askUserHandlerFactory)
		{
			this.httpListenerContext = httpListenerContext;
			this.logger = logger;
		}
		public async Task<bool> Handle(IError error, int statusCode, string statusText, string message)
		{
			return await base.Handle(error);
		}
		public async Task ShowError(IError error, int statusCode, string statusText, string message, GoalStep? step)
		{
			try
			{
				var resp = httpListenerContext.Response;

				resp.StatusCode = statusCode;
				resp.StatusDescription = statusText;
				
				using (var writer = new StreamWriter(resp.OutputStream, resp.ContentEncoding ?? Encoding.UTF8))
				{
					if (JsonHelper.IsJson(error.Message))
					{
						await writer.WriteAsync(error.Message);
					}
					else
					{
						await writer.WriteAsync(JsonConvert.SerializeObject(error.ToString(), Formatting.Indented));

					}
					await writer.FlushAsync();
				}
			}
			catch (ObjectDisposedException)
			{
				logger.LogWarning($"Object disposed when writing error to response:\n{error}");
			}

		}
	}
}
