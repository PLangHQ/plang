using PLang.Building.Model;
using PLang.Exceptions.AskUser;

namespace PLang.Errors.Handlers
{

	public class ConsoleErrorHandler : BaseErrorHandler, IErrorHandler
	{

		public ConsoleErrorHandler(IAskUserHandlerFactory askUserHandlerFactory) : base(askUserHandlerFactory)
		{
		}

		public async Task<bool> Handle(IError error)
		{
			return await base.Handle(error);
		}

		public async Task ShowError(IError error, GoalStep? step)
		{
			if (error.StatusCode < 200)
			{
				Console.ForegroundColor = ConsoleColor.Green;
			}
			else if (error.StatusCode >= 300 && error.StatusCode < 500)
			{
				Console.ForegroundColor = ConsoleColor.Yellow;
			}
			else if (error.StatusCode >= 500)
			{
				Console.ForegroundColor = ConsoleColor.Red;
			}
			Console.WriteLine(error.ToFormat().ToString());
			Console.ResetColor();
		}
	}
}
