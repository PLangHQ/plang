using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Interfaces;

namespace PLang.Modules.MessageModule
{
	public class Builder : BaseBuilder
	{
		private readonly ISettings settings;
		private readonly ILlmService llmService;

		public Builder(ISettings settings, ILlmService llmService)
		{
			this.settings = settings;
			this.llmService = llmService;
		}


		public override Task<Instruction> Build(GoalStep step)
		{
			var moduleSettings = new ModuleSettings(settings, llmService);
			var replays = moduleSettings.GetRelays();
			var accounts = moduleSettings.GetAccounts();
			AppendToAssistantCommand(@$"Following Relay servers are available: {JsonConvert.SerializeObject(replays)}.
Following are Nostr accounts:{JsonConvert.SerializeObject(accounts)}
");
			return base.Build(step);
		}
	}
}
