namespace PLang.Errors.Handlers
{
	public record AskUserWebserver(string Question, int StatusCode = 500, Func<object?, Task>? CallbackMethod = null) : AskUserError(Question, CreateAdapter(CallbackMethod))
    {
        public override async Task InvokeCallback(object value)
        {
            return;
        }
    }
}
