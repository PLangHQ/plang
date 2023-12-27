using Newtonsoft.Json;
using PLang.Interfaces;
using PLang.Utils;
using System.Net;
using System.Text;

namespace PLang.Exceptions.AskUser
{
	public class AskUserWebserverHandler : IAskUserHandler
	{
		private readonly HttpListenerContext context;

		public AskUserWebserverHandler(PLangAppContext context)
		{
			this.context = context[ReservedKeywords.HttpContext] as HttpListenerContext;
		}


		public async Task<bool> Handle(AskUserException ex)
		{
			int statusCode = (ex is AskUserWebserver) ? ((AskUserWebserver)ex).StatusCode : 500;
			var response = context.Response;
			response.StatusCode = statusCode;
			using (var writer = new StreamWriter(response.OutputStream, response.ContentEncoding ?? Encoding.UTF8))
			{
				string output = GetOutput(response, ex);
				await writer.WriteAsync(output);
				await writer.FlushAsync();
			}

			return false;
		}

		private string GetOutput(HttpListenerResponse response, AskUserException ex)
		{
			object objectToSerialize = AppContext.TryGetSwitch(ReservedKeywords.Debug, out var debug) ? ex : ex.Message;

			if (response.ContentType == "application/json")
			{
				return JsonConvert.SerializeObject(objectToSerialize);
			}
			return objectToSerialize.ToString();

		}
	}
}
