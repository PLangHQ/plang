namespace PLang.Errors
{
	public record AskUserError(string Message, Func<object[], Task<IError?>> Callback) : CallbackError(Message, Callback, AskUserError.Key)
	{
		public static readonly new string Key = "AskUser";

		public override async Task InvokeCallback(object value)
		{
			await Callback.Invoke([value]);
		}
	}

	public record FileAccessRequestError(string Message, Func<object?, Task<IError?>> callback) : CallbackError(Message, callback, FileAccessRequestError.Key)
	{
		public static readonly new string Key = "FileAccessRequest";
		public override async Task InvokeCallback(object value)
		{
			await Callback.Invoke([value]);
		}
	}
}
