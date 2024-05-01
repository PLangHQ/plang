using PLang.Errors;
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
		public async Task<Error?> Throw(string message, string type = "error", int statusCode = 400)
		{
			await outputStreamFactory.CreateHandler().Write(message, type, statusCode);
			if (statusCode >= 400)
			{
				return new EndGoal(goalStep, message ?? "");
			}
			return null;
		}

		[Description("When user intends the execution of the goal to stop without giving a error response. This is equal to doing return in a function")]
		public async Task<Error?> EndGoalExecution(string? message = null)
		{
			return new EndGoal(goalStep, message ?? "");
		}

	}
}
