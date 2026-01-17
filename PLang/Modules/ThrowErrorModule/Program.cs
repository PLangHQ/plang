using Microsoft.AspNetCore.Http;
using NBitcoin.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Errors.Types;
using PLang.Events;
using PLang.Models;
using PLang.Services.OutputStream;
using PLang.Services.OutputStream.Messages;
using PLang.Utils;
using System.ComponentModel;
using System.Diagnostics.Contracts;

namespace PLang.Modules.ThrowErrorModule
{
	[Description("statusCode default is 400")]
	public record ErrorInfo(string? errorMessage = null, string type = "error", int statusCode = 400);

	[Description("Allows user to throw error or retry a step. Allows user to return out of goal or stop(end) running goal. Create payment request(status code 402)")]
	public class Program : BaseProgram
	{
		private readonly ProgramFactory programFactory;

		public Program(ProgramFactory programFactory)
		{
			this.programFactory = programFactory;
		}


		[Description("When user intends to throw an error or critical, etc. This can be stated as 'show error', 'throw crtical', 'print error', etc.")]
		[MethodSettings(CanBeAsync = false, CanHaveErrorHandling = false, CanBeCached = false)]
		public async Task<IError?> ThrowError(ErrorMessage errorMessage)
		{
			var template = GetProgramModule<TemplateEngineModule.Program>();
			string content;
			IError? error = null;
			if (PathHelper.IsTemplateFile(errorMessage.Content))
			{
				(content, error) = await template.RenderFile(errorMessage.Content);
			} else
			{
				(content, error) = await template.RenderContent(errorMessage.Content);
			}
			if (error != null) return error;
			errorMessage = errorMessage with { Content = content };

			return new UserInputError(errorMessage.Content, goalStep, errorMessage.Key, errorMessage.StatusCode, null, errorMessage.FixSuggestion, errorMessage.HelpfullLinks, null, errorMessage);
		}

		[Description("When user intends to throw an error or critical, etc. This can be stated as 'show error', 'throw crtical', 'print error', etc. type can be error|critical. statusCode(like http status code) should be defined by user. error is %!error% if user defines it")]
		[MethodSettings(CanBeAsync = false, CanHaveErrorHandling = false, CanBeCached = false)]
		public async Task<IError?> Throw(object? message, string key = "UserDefinedError", int statusCode = 400, string? fixSuggestion = null, string? helpfullLinks = null)
		{
			if (message is IError) return message as IError;

			string errorMessage = (message == null) ? "UserDefinedError" : message.ToString() ?? "UserDefinedError";
			var em = new ErrorMessage(errorMessage, key, "error", statusCode, FixSuggestion: fixSuggestion, HelpfullLinks: helpfullLinks);
			return new UserInputError(errorMessage, goalStep, key, statusCode, null, fixSuggestion, helpfullLinks, ErrorMessage: em);
			
		}
		 

		[Description("Retries a step that caused an error. maxRetriesReachedMesage can contain {0} to include the retry count, when null a default message will be provided")]
		public async Task<IError?> Retry(int maxRetries = 1, string? maxRetriesReachedMesage = null, string key = "MaxRetries", int statusCode = 400, string? fixSuggestion = null, string? helpfullLinks = null)
		{
			var error = context.Error;
			if (error == null) return new ProgramError("No error available. Cannot retry a step when there is no error");
			if (error.Step == null) return new ProgramError("No step available. Cannot retry a step when I dont know which step to retry");

			if (error.Step.RetryCount >= maxRetries)
			{
				if (string.IsNullOrEmpty(maxRetriesReachedMesage))
				{
					maxRetriesReachedMesage = $"Max retries reached({error.Step.RetryCount})";
				} else if (maxRetriesReachedMesage.Contains("{0}"))
				{
					maxRetriesReachedMesage = maxRetriesReachedMesage.Replace("{0}", error.Step.RetryCount.ToString());
				}

					return await Throw(maxRetriesReachedMesage, key, statusCode, fixSuggestion, helpfullLinks);
			}

			error.Step.Retry = true;
			error.Step.RetryCount++;
			return null;
		}

		[Description("When user intends the execution of the goal to stop without giving a error response. This is equal to doing return in a function. Depth is how far up the stack it should end, previous goal is 1")]
		[MethodSettings(CanBeAsync = false, CanHaveErrorHandling = false, CanBeCached = false)]
		public async Task<IError?> EndGoalExecution(string? message = null, int levels = 0)
		{
			var endingGoal = goalStep.Goal;
			while (levels-- > 0)
			{
				if (endingGoal != null && endingGoal.ParentGoal != null)
				{
					endingGoal = endingGoal.ParentGoal;
				}
			}
			if (endingGoal == null) endingGoal = goal;

			return new EndGoal(false, endingGoal, goalStep, message ?? "", Levels: levels);
		}

		[Description("Shutdown the application")]
		[MethodSettings(CanBeAsync = false, CanHaveErrorHandling = false, CanBeCached = false)]
		public async Task EndApp()
		{
			Environment.Exit(0);
		}

		[Description("Create payment request(402)")]
		public async Task<(PaymentContract?, IError?)> CreatePaymentRequest(string name, string description, string error, List<Dictionary<string, object>> services)
		{
			if (appContext.ContainsKey("!contract"))
			{
				var strContract = appContext["!contract"]?.ToString();
				if (!string.IsNullOrEmpty(strContract))
				{
					var paymentContract = JObject.Parse(strContract).ToObject<PaymentContract>();
					return (paymentContract, null);
				}
			}
			string path = "/";
			if (HttpContext != null)
			{
				path = HttpContext.Request.Path;
			}
			var callback = await StepHelper.GetCallback(path, new(), memoryStack, goalStep, programFactory);

			var obj = new PaymentContract(name, description, error, services);
			
			var paymentRequest = new PaymentRequest(obj, callback);
			return (null, new PaymentRequestError(goalStep, obj, description, Callback: callback));

		}

	}

}
