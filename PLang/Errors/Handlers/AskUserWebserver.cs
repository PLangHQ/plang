using PLang.Errors.AskUser;

namespace PLang.Errors.Handlers
{
    public record AskUserWebserver(string Question, int StatusCode = 500, Func<object?, Task>? CallbackMethod = null) : AskUser.AskUserError(Question, CreateAdapter(CallbackMethod))
    {
        public override async Task<(bool, IError?)> InvokeCallback(object[]? value)
        {
            return (true, null);
        }
    }
}
