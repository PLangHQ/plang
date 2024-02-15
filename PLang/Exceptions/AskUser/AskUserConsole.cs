namespace PLang.Exceptions.AskUser
{
	public class AskUserConsole : AskUserException
	{
		public AskUserConsole(string question, Func<object?, Task>? callback = null) : base(question, callback)
		{
		}
		public override async Task InvokeCallback(object value)
		{
			await Callback.Invoke([value]);
		}
	}
}
