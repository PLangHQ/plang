using PLang.Errors.Builder;

namespace PLang.Errors.AskUser;

public record AskUserError(string Message, Func<object[]?, Task<(bool, IError?)>> Callback)
    : CallbackError(Message, Callback, Key), IError, IBuilderError
{
    public new static readonly string Key = "AskUser";

    public bool ContinueBuild => false;

    public override async Task<(bool, IError?)> InvokeCallback(object[]? value)
    {
        return await Callback.Invoke(value);
    }
}