namespace PLang.Errors
{
	public abstract record CallbackError(string Message, Func<object[], Task<IError?>> Callback, string? Key = null) : Error(Message, Key)
	{
		public abstract Task InvokeCallback(object value);

		protected static Func<object[], Task<IError?>> CreateAdapter(Delegate? callback)
		{
			if (callback == null) { return (obj) => { return null; }; }
			return async args =>
			{
				var result = callback.DynamicInvoke(args) as Task<IError>;
				if (result == null) return null;

				await result;
				if (result.Exception != null) throw result.Exception;
				return result.Result;

			};
		}
	}
}
