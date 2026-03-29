using System.Text.Json;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Utility;

namespace PLang.Runtime2.modules.builder;

/// <summary>
/// Saves AppData back to disk.
/// </summary>
[Action("saveApp")]
public partial class saveApp : IContext
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

        var fs = engine.FileSystem;
        var savePath = string.IsNullOrWhiteSpace(Path) ? ".build/app.pr" : Path;
        var absPath = fs.Path.GetFullPath(fs.Path.Combine(engine.AbsolutePath, savePath));

        try
        {
            var dir = fs.Path.GetDirectoryName(absPath)!;
            if (!fs.Directory.Exists(dir))
                fs.Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(App, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
            fs.File.WriteAllText(absPath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new Engine.Errors.ActionError(
                $"Failed to save app.pr: {ex.Message}", "IOError", 500));
        }

        return Data.Ok(App);
    }
}
