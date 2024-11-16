using PLang.Building.Model;
using PLang.Errors.Builder;
using PLang.Interfaces;

namespace PLang.Modules.CryptographicModule;

public class Builder : BaseBuilder
{
    private readonly ModuleSettings moduleSettings;

    public Builder(ISettings settings)
    {
        moduleSettings = new ModuleSettings(settings);
    }

    public override async Task<(Instruction?, IBuilderError?)> Build(GoalStep step)
    {
        var names = string.Join(", ", moduleSettings.GetBearerTokenSecrets().Select(p => p.Name));
        AppendToAssistantCommand($"Bearer token names are: {names}");
        return await base.Build(step);
    }
}