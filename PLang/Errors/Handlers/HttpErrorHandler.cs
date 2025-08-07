using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Exceptions.AskUser;
using PLang.Models.Formats;
using PLang.Modules;
using PLang.Modules.SerializerModule;
using PLang.Utils;
using System.Net;

namespace PLang.Errors.Handlers
{
	public class HttpErrorHandler : BaseErrorHandler, IErrorHandler
	{
		private readonly HttpContext httpContext;
		private readonly ILogger logger;
		private readonly ProgramFactory programFactory;

		public HttpErrorHandler(HttpContext httpContext, ILogger logger, ProgramFactory programFactory) : base()
		{
			this.httpContext = httpContext;
			this.logger = logger;
			this.programFactory = programFactory;
		}
		public async Task<(bool, IError?)> Handle(IError error)
		{
			return await base.Handle(error);
		}


		public async Task ShowError(IError error, GoalStep? step)
		{
			try
			{
				var resp = httpContext.Response;
				resp.StatusCode = error.StatusCode;


				var identity = programFactory.GetProgram<Modules.IdentityModule.Program>(step);
				/*var plangResponse = new PlangResponse()
				{
					Data = error.AsData(),
					Headers = null,
					Callback = await StepHelper.Callback(error.Step, programFactory)
				};*/

				var jsonError = new JsonRpcError()
				{
					Code = error.StatusCode,
					Message = error.Message,
					Data = error.AsData()
				};

				JsonRpcErrorResponse jsonRpc = new()
				{
					Error = jsonError,
				};

				Payload payload = new Payload()
				{
					Response = jsonRpc,
					Signature = await identity.Sign(jsonRpc)
				};

				var result = JsonConvert.SerializeObject(payload);
				var result2 = System.Text.Json.JsonSerializer.Serialize(payload);
				await programFactory.GetProgram<Modules.SerializerModule.Program>(step).Serialize(payload, stream: resp.Body);
			}
			catch (ObjectDisposedException)
			{
				logger.LogWarning($"Object disposed when writing error to response:\n{error}");
			} catch (Exception ex)
			{
				Console.WriteLine(error);

				Console.WriteLine("------------------------- Exception -------------------");
				Console.WriteLine(ex);
			}

		}
	}
}
