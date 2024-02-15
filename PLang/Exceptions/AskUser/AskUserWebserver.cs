namespace PLang.Exceptions.AskUser
{
	public class AskUserWebserver : AskUserException
	{
		public int StatusCode { get; private set; }
		public AskUserWebserver(string question, int statusCode = 500, Func<object?, Task>? callback = null) : base(question, callback)
		{
			StatusCode = statusCode;
		}

		public override async Task InvokeCallback(object value)
		{
			return;
		}
	}
}
