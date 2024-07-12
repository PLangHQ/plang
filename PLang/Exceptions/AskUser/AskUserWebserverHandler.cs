using Newtonsoft.Json;
using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Errors.Handlers;
using PLang.Interfaces;
using PLang.Services.OutputStream;
using PLang.Utils;
using System.Net;
using System.Text;

namespace PLang.Exceptions.AskUser
{
	/*
    public class AskUserWebserverHandler : IAskUserHandler
	{
		private readonly HttpListenerContext context;
		private readonly IOutputStreamFactory outputStreamFactory;

		public AskUserWebserverHandler(PLangAppContext context, IOutputStreamFactory outputStreamFactory)
		{
			if (context.TryGetValue(ReservedKeywords.HttpContext, out object? obj) && obj != null)
			{
				this.context = (HttpListenerContext)obj;
			}

			if (this.context == null) throw new NullReferenceException($"{nameof(context)} cannot be null");
			this.outputStreamFactory = outputStreamFactory;
		}


		public async Task<(bool, IError?)> Handle(AskUserError error)
		{
			int statusCode = (error is AskUserWebserver) ? ((AskUserWebserver)error).StatusCode : 500;
			var response = context.Response;
			response.StatusCode = statusCode;

			await outputStreamFactory.CreateHandler().Write(error, error.Message, statusCode);
			
			return (true, null);
		}

	}*/
}
