using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Errors.Builder;
using PLang.Interfaces;
using PLang.Services.LlmService;

namespace PLang.Modules.MessageModule;

public class Builder : BaseBuilder
{
    private readonly ILlmServiceFactory llmServiceFactory;
    private readonly ISettings settings;

    public Builder(ISettings settings, ILlmServiceFactory llmServiceFactory)
    {
        this.settings = settings;
        this.llmServiceFactory = llmServiceFactory;
    }


    public override Task<(Instruction? Instruction, IBuilderError? BuilderError)> Build(GoalStep step)
    {
        var moduleSettings = new ModuleSettings(settings, llmServiceFactory);
        var replays = moduleSettings.GetRelays();
        var accounts = moduleSettings.GetAccounts();
        AppendToAssistantCommand(@$"Following Relay servers are available: {JsonConvert.SerializeObject(replays)}.
Following are Nostr accounts:{JsonConvert.SerializeObject(accounts)}
");
        return base.Build(step);
    }
}