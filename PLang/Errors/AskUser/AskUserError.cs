using PLang.Errors.Builder;

namespace PLang.Errors.AskUser
{
	public record AskUserError(string Actor, string Channel, string Message, Func<object[]?, Task<(bool, IError?)>> Callback) : CallbackError(Actor, Channel, Message, Callback, AskUserError.Key), IError, IBuilderError
    {
        public static readonly new string Key = "AskUser";

        public bool ContinueBuild => false;

		public bool Retry => false;

		public string? LlmBuilderHelp { get; set; }

		public override async Task<(bool, IError?)> InvokeCallback(object[]? value)
        {
            return await Callback.Invoke(value);
        }
    }


}
