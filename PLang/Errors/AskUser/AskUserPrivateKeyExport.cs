using PLang.Errors.Builder;
using PLang.Interfaces;
using PLang.Models;
using PLang.Services.LlmService;
using PLang.Utils;

namespace PLang.Errors.AskUser;

public record AskUserPrivateKeyExport(
    ILlmServiceFactory llmServiceFactory,
    ISettings settings,
    string PrivateKeyNamespace) : AskUserError(Message, null), IError, IBuilderError
{
    public new static readonly string Key = "AskUserPrivateKeyExport";
    public static readonly string LockedKey = "LockedAskUserPrivateKeyExport";

    private new static readonly string Message =
        @"Before we export your private keys I would like to ask you 3 question. Remember never share your private keys with people you don't know or trust.
Question 1: Why are you sharing your private key?";

    private readonly List<string> answers = new();
    public new bool ContinueBuild => true;

    public override async Task<(bool, IError?)> InvokeCallback(object[]? value)
    {
        answers.Clear();
        return await GetSecondQuestion(value[0].ToString());
    }

    private async Task<(bool, IError?)> GetSecondQuestion(string answer)
    {
        answers.Add("1. " + answer);
        return (false,
            new AskUserPrivateKeyError(@"2. Who specifically requested your private key, and how did they contact you?",
                GetThirdQuestion));
    }

    private async Task<(bool, IError?)> GetThirdQuestion(string answer)
    {
        answers.Add("2. " + answer);
        return (false,
            new AskUserPrivateKeyError(
                @"3. Were you promised any benefits, rewards, or solutions in return for your private key?",
                MakeDecision));
    }

    private async Task<(bool, IError?)> MakeDecision(string answer)
    {
        answers.Add("3. " + answer);

        var system = @"User is about to export his private keys. 
I have asked him 3 questions to determine if he is being scammed.
Give 3 levels of likely hood of him being scammed, low, medium, high. 
Give max 140 character description to the user about securing the private keys
Expires is default null, unless defined by user.

These are the 3 questions
1. Why are you planning to share your private key?
2. Who specifically requested your private key, and how did they contact you?
3. Were you promised any benefits, rewards, or solutions in return for your private key?
";

        var promptMessage = new List<LlmMessage>();
        promptMessage.Add(new LlmMessage("system", system));
        promptMessage.Add(new LlmMessage("user", string.Join("\n", answers)));

        var llmRequest = new LlmRequest(PrivateKeyNamespace, promptMessage);
        var (response, queryError) = await llmServiceFactory.CreateHandler().Query<DecisionResponse>(llmRequest);

        if (queryError != null) return (false, queryError);
        if (response == null) return (false, new Error("Could not get response from LLM. Try again"));

        if (response.Expires == null) response.Expires = SystemTime.UtcNow().AddSeconds(10);

        settings.Set<DecisionResponse>(typeof(AskUserPrivateKeyExport), PrivateKeyNamespace, response);
        if (response.Level.ToLower() == "low" || response.Level.ToLower() == "medium") return (true, null);

        settings.Set(typeof(AskUserPrivateKeyExport), LockedKey, SystemTime.UtcNow().AddDays(1));
        return (true, null);
    }

    public class DecisionResponse(string Level, string Explain, DateTime? Expires)
    {
        public string Level { get; } = Level;
        public string Explain { get; } = Explain;
        public DateTime? Expires { get; set; } = Expires;
    }
}