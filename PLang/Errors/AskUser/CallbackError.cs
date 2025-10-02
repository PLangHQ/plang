namespace PLang.Errors.AskUser
{
    public interface ICallbackError { }

    public abstract record CallbackError(string Actor, string Channel, string Message, Func<object[]?, Task<(bool, IError?)>> Callback, string Key = "CallbackError") : Error(Message, Key), ICallbackError
	{
        public abstract Task InvokeCallback(object[]? value);

        public static Func<object[]?, Task<(bool, IError?)>> CreateAdapter(Delegate? callback)
        {
            if (callback == null) { return (obj) => { return null; }; }
            return async args =>
            {
                var result = callback.DynamicInvoke(args) as Task<(bool, IError?)>;
                if (result == null) return (false, null);

                await result;
                if (result.Exception != null) throw result.Exception;
                return result.Result;

            };
        }
    }
}
