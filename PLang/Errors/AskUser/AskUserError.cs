namespace PLang.Errors.AskUser
{
    public record AskUserError(string Message, Func<object[]?, Task<(bool, IError?)>> Callback) : CallbackError(Message, Callback, AskUserError.Key), IError
    {
        public static readonly new string Key = "AskUser";

        public override async Task<(bool, IError?)> InvokeCallback(object[]? value)
        {
            return await Callback.Invoke([value]);
        }
    }


}
