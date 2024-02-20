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

		public HttpExceptionHandler(HttpListenerContext httpListenerContext, IAskUserHandlerFactory askUserHandlerFactory) : base(askUserHandlerFactory)
		{
			this.httpListenerContext = httpListenerContext;
		}

		public async Task<bool> Handle(Exception exception, int statusCode, string statusText, string message)
		{
			if (await base.Handle(exception)) { return true; }

			var resp = httpListenerContext.Response;
			resp.StatusCode = statusCode;
			resp.StatusDescription = statusText;

			AppContext.TryGetSwitch(ReservedKeywords.Debug, out bool isDebug);

			var response = new Dictionary<string, object>();
			response.Add(statusText, message);
			if (isDebug)
			{
				response.Add("exception", exception);
			}

			try
			{
				using (var writer = new StreamWriter(resp.OutputStream, resp.ContentEncoding ?? Encoding.UTF8))
				{
					await writer.WriteAsync(JsonConvert.SerializeObject(response));
					await writer.FlushAsync();
				}
			} catch (Exception ex)
			{
				Console.WriteLine(JsonConvert.SerializeObject(response));
				Console.WriteLine(ex);
			
			}

			return false;
		}
	}
}
