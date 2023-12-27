namespace PLang.Exceptions.AskUser
{
	internal class AskUserPathIsMissingException : AskUserException
	{
		public AskUserPathIsMissingException(string message, Func<string, string>? callback = null) : base(message, CreateAdapter(callback))
		{
		}
		public override async Task InvokeCallback(object value)
		{
			await Callback?.Invoke(new object[] { value });
		}
	}
}
