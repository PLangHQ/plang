using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Utils;

namespace PLang.Exceptions.Handlers
{
	
	public class ConsoleExceptionHandler : ExceptionHandler, IExceptionHandler
	{
		private readonly IAskUserHandler askUserHandler;

		public ConsoleExceptionHandler(IAskUserHandler askUserHandler) : base(askUserHandler)
		{
			this.askUserHandler = askUserHandler;
		}
		public async Task<bool> Handle(Exception exception, int statusCode, string statusText, string message)
		{
			if (await base.Handle(exception)) { return true; }

			if (statusCode < 200)
			{
				Console.ForegroundColor = ConsoleColor.Green;
			}
			else if (statusCode >= 300 && statusCode < 500)
			{
				Console.ForegroundColor = ConsoleColor.Yellow;
			}
			else if (statusCode >= 500)
			{
				Console.ForegroundColor = ConsoleColor.Red;
			}
			AppContext.TryGetSwitch(ReservedKeywords.Debug, out bool isDebug);

			Console.WriteLine("\n\nError: " + message);
			if (exception is RuntimeProgramException rpe)
			{
				Console.WriteLine(rpe.ToString());
			}
			else
			{
				if (exception is RuntimeStepException rse)
				{
					var step = rse.Step;
					string errorInfo = $"GoalName '{step.Goal.GoalName}' at {step.Goal.AbsoluteGoalPath}";
					errorInfo += $"\nStep '{step.Text}'";
					Console.WriteLine(errorInfo);
				}
				
				Console.WriteLine("\n=== Stack trace ===");
				Console.WriteLine(exception.StackTrace);
				Console.WriteLine("\n\n=== Full Exception ===");
				Console.WriteLine(JsonConvert.SerializeObject(exception));
			}
			Console.ResetColor();
			return false;
		}
	}
}
