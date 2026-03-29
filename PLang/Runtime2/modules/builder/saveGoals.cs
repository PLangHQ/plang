using System.Text.Json;
using System.Text.Json.Serialization;
using PLang.Runtime2.Engine.Memory;
using Goal = PLang.Runtime2.Engine.Goals.Goal.@this;

namespace PLang.Runtime2.modules.builder;

/// <summary>
/// Serializes goals to a v0.2 .pr file.
/// One .goal file → one .pr file containing List&lt;Goal&gt;.
/// </summary>
[Action("saveGoals")]
public partial class saveGoals : IContext
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

        var fs = engine.FileSystem;
        var absPath = fs.Path.GetFullPath(
            fs.Path.Combine(engine.AbsolutePath, prPath.TrimStart('/', '\\')));

        try
        {
            var dir = fs.Path.GetDirectoryName(absPath)!;
            if (!fs.Directory.Exists(dir))
                fs.Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(Goals, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            });
            fs.File.WriteAllText(absPath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new Engine.Errors.ActionError(
                $"Failed to save .pr file: {ex.Message}", "IOError", 500));
        }

        return Data.Ok(true);
    }
}
