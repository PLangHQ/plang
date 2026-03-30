using System.Text.Json;
using PLang.Runtime2.Engine.Memory;
using Goal = PLang.Runtime2.Engine.Goals.Goal.@this;

namespace PLang.Runtime2.modules.builder;

/// <summary>
/// Serializes goals to a v0.2 .pr file via file.save.
/// Includes all properties (nulls included for determinism).
/// </summary>
[Action("goals.save")]
public partial class goalsSave : IContext
{
    [IsNotNull]
    public partial List<Goal> Goals { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine;
        if (!engine.Building.IsEnabled)
            return Data.FromError(new Engine.Errors.ActionError("Building is not enabled", "BuildingDisabled", 400));

        if (Goals.Count == 0)
            return Data.FromError(new Engine.Errors.ActionError("No goals to save", "NoGoals", 400));

        var prPath = Goals[0].PrPath;
        if (string.IsNullOrEmpty(prPath))
            return Data.FromError(new Engine.Errors.ActionError("Goals have no Path set, cannot derive PrPath", "NoPrPath", 400));

        var json = JsonSerializer.Serialize(Goals, JsonOptions.PrFile);

        var saveAction = new file.Save
        {
            Context = Context,
            Path = new PLangPath(prPath, Context),
            Value = new Data("", json)
        };
        var saveResult = await engine.RunAction(saveAction, Context);
        if (!saveResult.Success)
            return saveResult;

        return Data.Ok(true);
    }
}
