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
		public async Task<IError?> Throw(string message, string type = "error", int statusCode = 400)
		{
			await outputStreamFactory.CreateHandler().Write(message, type, statusCode);
			if (statusCode >= 300)
			{
				return new StepError(message, goalStep, type, statusCode);
			}
			return null;
		}

		[Description("When user intends the execution of the goal to stop without giving a error response. This is equal to doing return in a function")]
		public async Task<IError?> EndGoalExecution(string? message = null)
		{
			return new EndGoal(goalStep, message ?? "");
		}

		[Description("Shutdown the application")]
		public async Task EndApp()
		{
			Environment.Exit(0);
		}

	}
}
