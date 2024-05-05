using PLang.Errors;
using PLang.Interfaces;

namespace PLang.Exceptions.AskUser
{

	public class AskUserConsoleHandler : IAskUserHandler
	{

		public async Task<bool> Handle(AskUserError askUserError)
		{
			Console.WriteLine("\n\n----- Ask User -----\n" + askUserError.Message);
			var result = Console.ReadLine();

			if (askUserError.InvokeCallback != null)
			{
				await askUserError.InvokeCallback(result ?? "");
			}
			return true;

		}

	}
}
