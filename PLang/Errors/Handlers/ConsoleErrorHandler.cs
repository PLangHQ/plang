using PLang.Building.Model;
using PLang.Exceptions.AskUser;

namespace PLang.Errors.Handlers
{

	public class ConsoleErrorHandler : BaseErrorHandler, IErrorHandler
	{

		public ConsoleErrorHandler(IAskUserHandlerFactory askUserHandlerFactory) : base(askUserHandlerFactory)
		{
		}

		public async Task<bool> Handle(IError error, int statusCode, string statusText, string message)
		{
			return await base.Handle(error);
		}

		public async Task ShowError(IError error, int statusCode, string statusText, string message, GoalStep? step)
		{
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
			Console.WriteLine(error);
			Console.ResetColor();
		}
	}
}
