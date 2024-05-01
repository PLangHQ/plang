using PLang.Errors;
using PLang.Interfaces;

namespace PLang.Exceptions.AskUser
{

	public class AskUserConsoleHandler : IAskUserHandler
	{

		public async Task<bool> Handle(AskUserFileAccess ex)
		{
			Console.WriteLine("\n\n----- Ask User -----\n" + ex.Message);
			var result = Console.ReadLine();

			if (ex.InvokeCallback != null)
			{
				await ex.InvokeCallback(result ?? "");
			}
			return true;

		}

	}
}
