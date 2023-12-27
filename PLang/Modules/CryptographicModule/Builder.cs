using PLang.Building.Model;
using PLang.Interfaces;

namespace PLang.Modules.CryptographicModule
{
	public class Builder : BaseBuilder
    {
		ModuleSettings moduleSettings;
		public Builder(ISettings settings)
		{
			moduleSettings = new ModuleSettings(settings);
		}
		public override async Task<Instruction> Build(GoalStep step)
		{
			string names = string.Join(", ", moduleSettings.GetBearerTokenSecrets().Select(p => p.Name));
			AppendToAssistantCommand($"Bearer token names are: {names}");
			return await base.Build(step);
		}

	}
}

