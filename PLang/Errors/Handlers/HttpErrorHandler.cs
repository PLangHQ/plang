using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Models.Formats;
using PLang.Modules;
using PLang.Modules.SerializerModule;
using PLang.Runtime;
using PLang.Utils;
using System.Net;

namespace PLang.Errors.Handlers
{
	public class HttpErrorHandler : BaseErrorHandler, IErrorHandler
	{
		private readonly HttpContext httpContext;
		private readonly ILogger logger;
		private readonly IModuleRegistry moduleRegistry;

		public HttpErrorHandler(HttpContext httpContext, ILogger logger, IModuleRegistry moduleRegistry) : base()
		{
			this.httpContext = httpContext;
			this.logger = logger;
			this.moduleRegistry = moduleRegistry;
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


				var (identity, identityError) = moduleRegistry.Get<Modules.IdentityModule.Program>();
				if (identityError != null)
				{
					logger.LogError($"Failed to get IdentityModule: {identityError.Message}");
					return;
				}
				/*var plangResponse = new PlangResponse()
				{
					Data = error.AsData(),
					Headers = null,
					Callback = await StepHelper.Callback(error.Step, moduleRegistry)
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
					Signature = await identity!.Sign(jsonRpc)
				};

				var result = JsonConvert.SerializeObject(payload);
				var result2 = System.Text.Json.JsonSerializer.Serialize(payload);
				var (serializer, serializerError) = moduleRegistry.Get<Modules.SerializerModule.Program>();
				if (serializerError != null)
				{
					logger.LogError($"Failed to get SerializerModule: {serializerError.Message}");
					return;
				}
				await serializer!.Serialize(payload, stream: resp.Body);
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
