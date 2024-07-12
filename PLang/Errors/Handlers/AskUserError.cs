namespace PLang.Errors.Handlers
{

    public abstract class AskUserError : Exception
    {
        protected Func<object[], Task> Callback { get; set; }
        public abstract Task InvokeCallback(object value);
        public AskUserError(string question) : base(question)
        {
            Callback = (obj) => { return Task.CompletedTask; };
        }
        public AskUserError(string question, Func<object[], Task> callback) : base(question)
        {
            Callback = callback;
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
