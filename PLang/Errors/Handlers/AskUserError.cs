namespace PLang.Errors.Handlers
{

	public abstract class AskUserError : Exception
	{
		protected Func<object[], Task<IError?>> Callback { get; set; }
		public abstract Task<IError?> InvokeCallback(object value);
		public AskUserError(string question) : base(question)
		{
			Callback = (obj) => { return Task.FromResult<IError?>(null); };
		}
		public AskUserError(string question, Func<object[], Task<IError?>> callback) : base(question)
		{
			Callback = callback;
		}

		protected static Func<object[], Task<IError?>> CreateAdapter(Delegate? callback)
		{
			if (callback == null) { return (obj) => { return Task.FromResult<IError?>(null); }; }
			return async args =>
			{
				var task = callback.DynamicInvoke(args) as Task<IError?>;
				if (task == null) return null;

				var result = await task;
				if (task.Exception != null) throw task.Exception;
				return result;
			};
		}

	}



}
