using PLang.Errors.AskUser;

namespace PLang.Errors.Handlers
{
    public record AskUserWebserver(string Question, int StatusCode = 500, Func<object?, Task>? CallbackMethod = null, string Actor = "system", string Channel = "default") : AskUser.AskUserError(Actor, Channel, Question, CreateAdapter(CallbackMethod))
    {
        public override async Task<(bool, IError?)> InvokeCallback(object[]? value)
        {
            return (true, null);
        }
    }
}
