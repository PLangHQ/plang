using PLang.Errors;
using PLang.SafeFileSystem;

namespace PLang.Exceptions.AskUser
{

	public record AskUserFileAccess(string App, string Path, string Message, Func<string, string, string?, Task<IError?>> CallbackMethod) : AskUserError(Message, CreateAdapter(CallbackMethod))
	{
		public override async Task<IError?> InvokeCallback(object answer)
		{
			return await Callback.Invoke([App, Path, answer.ToString()]);

		}
	}

}
