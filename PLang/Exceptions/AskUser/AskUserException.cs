namespace PLang.Exceptions.AskUser
{
	public abstract class AskUserException : Exception
	{
		protected Func<object[], Task> Callback { get; set; }
		public abstract Task InvokeCallback(object value);
		public AskUserException(string question) : base(question)
		{
			this.Callback = (obj) => { return Task.CompletedTask; };
		}
		public AskUserException(string question, Func<object[], Task> callback) : base(question)
		{
			this.Callback = callback;
		}

		protected static Func<object[], Task> CreateAdapter(Delegate? callback)
		{
			if (callback == null) { return (obj) => { return Task.CompletedTask; }; }
			return async args =>
			{
				var result = callback.DynamicInvoke(args) as Task;
				if (result != null)
				{
					await result;
					if (result.Exception != null) throw result.Exception;
				}

				
			};
		}

	}

	

}
