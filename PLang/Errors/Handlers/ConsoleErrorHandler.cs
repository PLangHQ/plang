using PLang.Building.Model;
using PLang.Exceptions.AskUser;

namespace PLang.Errors.Handlers
{

	public class ConsoleErrorHandler : BaseErrorHandler, IErrorHandler
	{

		public ConsoleErrorHandler() : base()
		{
		}

		public async Task<(bool, IError?)> Handle(IError error)
		{
			Console.WriteLine(error.ToString());
			return (true, null);
			
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
