﻿using PLang.SafeFileSystem;

namespace PLang.Errors.AskUser
{
    public record FileAccessRequestError(string Message, string AppName, string Path, string Key = "FileAccessRequest", int StatusCode = 400, string? FixSuggestion = null, string? HelpfulLinks = null) :
        Error(Message, Key, StatusCode, null, FixSuggestion, HelpfulLinks)
    {

    }

    public record AskUserFileAccess(string App, string Path, string Message, Func<string, string, string?, Task<(bool, IError?)>> CallbackMethod) : AskUserError(Message, CreateAdapter(CallbackMethod))
    {
        public override async Task<(bool, IError?)> InvokeCallback(object?[] answer)
        {
            return await CallbackMethod.Invoke(App, Path, answer[0].ToString());

        }
    }

}
