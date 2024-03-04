using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Utils;

namespace PLang.Exceptions.Handlers
{
	
	public class ConsoleExceptionHandler : ExceptionHandler, IExceptionHandler
	{

		public ConsoleExceptionHandler(IAskUserHandlerFactory askUserHandlerFactory) : base(askUserHandlerFactory)
		{
		}

		public async Task<bool> Handle(Exception exception, int statusCode, string statusText, string message)
		{
			return await base.Handle(exception);
		}

		public async Task<bool> ShowError(Exception exception, int statusCode, string statusText, string message, GoalStep? step)
		{
			//if (await base.Handle(exception)) { return true; }

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

			
			if (exception is RuntimeProgramException rpe)
			{
				
				Console.WriteLine(rpe.ToString());
				Console.WriteLine("\n\nError: " + message);

			}
			else
			{
				if (exception is BaseStepException rse)
				{
					step = rse.Step;					
				}
				if (step != null)
				{
					string errorInfo = $"\n\n === Error Info === \nGoalName '{step.Goal.GoalName}' at {step.Goal.AbsoluteGoalPath}";
					errorInfo += $"\nStep '{step.Text}'";
					Console.WriteLine(errorInfo);
				}

				Console.WriteLine("\n\nError: " + message);
				Console.WriteLine("\n=== Stack trace ===");
				Console.WriteLine(exception.StackTrace);
			}
			Console.ResetColor();
			return false;
		}
	}
}
