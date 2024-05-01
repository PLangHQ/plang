using PLang.Errors;

namespace PLang.Exceptions.AskUser
{

	public record AskUserFileAccess(string App, string Path, string Message, Func<object[], Task<IError?>> Callback) : AskUserError(Message, CreateAdapter(Callback))
	{
		//public new Func<string, string, string, Task<IError?>>? Callback { get; init; }
		public override async Task InvokeCallback(object answer)
		{
			await Callback.Invoke([App, Path, Message]);

		}
	}

}
