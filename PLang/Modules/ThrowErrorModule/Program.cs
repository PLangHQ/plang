﻿using NBitcoin.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Errors.Types;
using PLang.Models;
using PLang.Services.OutputStream;
using PLang.Utils;
using System.ComponentModel;
using System.Diagnostics.Contracts;

namespace PLang.Modules.ThrowErrorModule
{
	public record ErrorInfo(string? errorMessage = null, string type = "error", int statusCode = 400);

	[Description("Allows user to throw error or retry a step. Allows user to return out of goal or stop(end) running goal. Create payment request(status code 402)")]
	public class Program : BaseProgram
	{
		private readonly IOutputStreamFactory outputStreamFactory;
		private readonly ProgramFactory programFactory;

		public Program(IOutputStreamFactory outputStreamFactory, ProgramFactory programFactory)
		{
			this.outputStreamFactory = outputStreamFactory;
			this.programFactory = programFactory;
		}

		[Description("When user intends to throw an error or critical, etc. This can be stated as 'show error', 'throw crtical', 'print error', etc. type can be error|critical. statusCode(like http status code) should be defined by user.")]
		[MethodSettings(CanBeAsync = false, CanHaveErrorHandling = false, CanBeCached = false)]
		public async Task<IError?> Throw(object? message, string type = "error", int statusCode = 400)
		{
			if (message is IError) return message as IError;
			//await outputStreamFactory.CreateHandler().Write(message, type, statusCode);
			return new UserInputError(message.ToString(), goalStep, type, statusCode);
		}

		[Description("Retries a step that caused an error")]
		public async Task<IError?> Retry()
		{
			var error = goal.GetVariable<IError>(ReservedKeywords.Error);
			if (error == null) return new ProgramError("No error available. Cannot retry a step when there is no error");
			if (error.Step == null) return new ProgramError("No step available. Cannot retry a step when I dont know which step to retry");

			error.Step.Retry = true;
			return null;
		}

		[Description("When user intends the execution of the goal to stop without giving a error response. This is equal to doing return in a function. Depth is how far up the stack it should end, previous goal is 1")]
		[MethodSettings(CanBeAsync = false, CanHaveErrorHandling = false, CanBeCached = false)]
		public async Task<IError?> EndGoalExecution(string? message = null, int levels = 0)
		{
			return new EndGoal(goalStep, message ?? "", Levels: levels);
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
			if (context.ContainsKey("!contract"))
			{
				var strContract = context["!contract"]?.ToString();
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
