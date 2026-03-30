using System.Text.Json;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Utility;

namespace PLang.Runtime2.modules.builder;

/// <summary>
/// Loads or creates .build/app.pr. Returns AppData.
/// </summary>
[Action("app")]
public partial class app : IContext
{
    [Default(".")]
    public partial string Path { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine;
        if (!engine.Building.IsEnabled)
            return Data.FromError(new Engine.Errors.ActionError("Building is not enabled", "BuildingDisabled", 400));

        var basePath = string.IsNullOrWhiteSpace(Path) || Path == "." ? "" : Path;
        var appPrPath = string.IsNullOrEmpty(basePath)
            ? ".build/app.pr"
            : basePath.TrimEnd('/', '\\') + "/.build/app.pr";

        // Try reading existing app.pr via file.read
        var readAction = new file.Read
        {
            Context = Context,
            Path = new PLangPath(appPrPath, Context)
        };
        var readResult = await engine.RunAction(readAction, Context);

        var json = readResult.Value switch
        {
            string s => s,
            byte[] b => System.Text.Encoding.UTF8.GetString(b),
            _ => readResult.Value?.ToString()
        };
        if (readResult.Success && !string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var existing = JsonSerializer.Deserialize<AppData>(json, JsonOptions.CaseInsensitive);
                if (existing != null)
                    return Data.Ok(existing);
            }
            catch (JsonException)
            {
                // Corrupt — create fresh
            }
        }

        // Create new
        var newApp = new AppData
        {
            Id = Guid.NewGuid().ToString(),
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow,
            Version = "0.2"
        };

        // Save via file.save
        var saveJson = JsonSerializer.Serialize(newApp, JsonOptions.CamelCase);
        var saveAction = new file.Save
        {
            Context = Context,
            Path = new PLangPath(appPrPath, Context),
            Value = new Data("", saveJson)
        };
        var saveResult = await engine.RunAction(saveAction, Context);
        if (!saveResult.Success)
            return saveResult;

        return Data.Ok(newApp);
    }
}
