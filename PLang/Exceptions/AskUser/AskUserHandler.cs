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


		public async Task<bool> Handle(AskUserException ex)
		{
			var result = outputStream.Ask(ex.Message, "ask", 200);

			if (ex.InvokeCallback != null)
			{
				await ex.InvokeCallback(result);
			}
			return true;
		}
	}
}
