using System.Text.Json;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Utility;

namespace PLang.Runtime2.modules.builder;

/// <summary>
/// Saves AppData back to disk via file.save.
/// </summary>
[Action("app.save")]
public partial class appSave : IContext
{
    [IsNotNull]
    public partial AppData App { get; init; }

    [Default(".build/app.pr")]
    public partial string Path { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine;
        if (!engine.Building.IsEnabled)
            return Data.FromError(new Engine.Errors.ActionError("Building is not enabled", "BuildingDisabled", 400));

        App.Updated = DateTime.UtcNow;

        var savePath = string.IsNullOrWhiteSpace(Path) ? ".build/app.pr" : Path;
        var json = JsonSerializer.Serialize(App, JsonOptions.CamelCase);

        var saveAction = new file.Save
        {
            Context = Context,
            Path = new PLangPath(savePath, Context),
            Value = new Data("", json)
        };
        var saveResult = await engine.RunAction(saveAction, Context);
        if (!saveResult.Success)
            return saveResult;

        return Data.Ok(App);
    }
}
