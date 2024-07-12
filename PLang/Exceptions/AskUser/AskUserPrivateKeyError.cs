using PLang.Errors.Handlers;

namespace PLang.Exceptions.AskUser
{

	public class AskUserPrivateKeyError : AskUserError
	{

		public AskUserPrivateKeyError(string message, Func<string, Task>? callback = null) : base(message, CreateAdapter(callback))
		{

		}

		public override async Task InvokeCallback(object answer)
		{
			await Callback.Invoke([answer]);
			
		}
	}
}
