using PLang.Errors;
using PLang.Interfaces;
using PLang.Services.OutputStream;

namespace PLang.Exceptions.AskUser
{
	public class AskUserHandler : IAskUserHandler
	{
		private readonly IOutputStream outputStream;

		public AskUserHandler(IOutputStream outputStream)
		{
			this.outputStream = outputStream;
		}


		public async Task<bool> Handle(AskUserError error)
		{
			var result = outputStream.Ask(error.Message, "ask", 200);

			if (error.InvokeCallback != null)
			{
				await error.InvokeCallback(result);
			}
			return true;
		}
	}
}
