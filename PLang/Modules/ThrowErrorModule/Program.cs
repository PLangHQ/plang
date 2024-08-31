using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Services.OutputStream;
using System.ComponentModel;

namespace PLang.Modules.ThrowErrorModule
{
	[Description("Allows user to throw error. Allows user to return out of goal or stop(end) running goal")]
	public class Program : BaseProgram
	{
		private readonly IOutputStreamFactory outputStreamFactory;

		public Program(IOutputStreamFactory outputStreamFactory)
		{
			this.outputStreamFactory = outputStreamFactory;
		}

		[Description("When user intends to throw an error or critical, etc. This can be stated as 'show error', 'throw crtical', 'print error', etc. type can be error|critical. statusCode(like http status code) should be defined by user.")]
		[MethodSettings(CanBeAsync = false, CanHaveErrorHandling = false, CanBeCached = false)]
		public async Task<IError?> Throw(object? message, string type = "error", int statusCode = 400)
		{
			if (message is IError) return message as IError;
			//await outputStreamFactory.CreateHandler().Write(message, type, statusCode);
			return new UserDefinedError(message.ToString(), goalStep, type, statusCode);
			
			
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

	}
}
