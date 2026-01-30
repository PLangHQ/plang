using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Builder;
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
		public override async Task<(Instruction?, IBuilderError?)> Build(GoalStep step, IBuilderError? previousBuildError = null)
		{
			string names = string.Join(", ", moduleSettings.GetSecrets().Select(p => p.Name));
			AppendToAssistantCommand($"Bearer token names are: {names}");
			return await base.Build(step, previousBuildError);
		}

	}
}

