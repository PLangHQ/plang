using PLang.Errors.Builder;
using PLang.SafeFileSystem;

namespace PLang.Errors.AskUser
{
    public record AskUserSettingsError(string Message, string Key = "AskUserSettings", int StatusCode = 401, string? FixSuggestion = null, string? HelpfulLinks = null) :
        Error(Message, Key, StatusCode, null, FixSuggestion, HelpfulLinks)
    {

    }

	public record AskUserSettingsBuilderError(string Message, string Key = "AskUserSettings", int StatusCode = 401, string? FixSuggestion = null, string? HelpfulLinks = null) :
	   BuilderError(Message, Key, StatusCode, false, null, FixSuggestion, HelpfulLinks)
	{

	}

	public record AskUserSettingsResponse(string App, string Path, string Message, Func<string, string, string?, Task<(bool, IError?)>> CallbackMethod,
		string Actor = "system", string Channel = "default") : AskUserError(Actor, Channel, Message, CreateAdapter(CallbackMethod))
    {
        public override async Task<(bool, IError?)> InvokeCallback(object?[] answer)
        {
            return await CallbackMethod.Invoke(App, Path, answer[0].ToString());

        }
    }

}
