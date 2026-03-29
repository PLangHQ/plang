using System.Text.Json;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Utility;

namespace PLang.Runtime2.modules.builder;

/// <summary>
/// Loads or creates .build/app.pr. Returns AppData.
/// </summary>
[Action("getApp")]
public partial class getApp : IContext
{
    [Default(".")]
    public partial string Path { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine;
        if (!engine.Building.IsEnabled)
            return Data.FromError(new Engine.Errors.ActionError("Building is not enabled", "BuildingDisabled", 400));

        var fs = engine.FileSystem;
        var basePath = string.IsNullOrWhiteSpace(Path) || Path == "." ? engine.AbsolutePath : Path;
        var appPrPath = fs.Path.Combine(basePath, ".build", "app.pr");

        if (fs.File.Exists(appPrPath))
        {
            try
            {
                var json = fs.File.ReadAllText(appPrPath);
                var app = JsonSerializer.Deserialize<AppData>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (app != null)
                    return Data.Ok(app);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                // Corrupt or unreadable — create fresh
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

        // Save it
        try
        {
            var buildDir = fs.Path.GetDirectoryName(appPrPath)!;
            if (!fs.Directory.Exists(buildDir))
                fs.Directory.CreateDirectory(buildDir);

            var json = JsonSerializer.Serialize(newApp, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
            fs.File.WriteAllText(appPrPath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new Engine.Errors.ActionError(
                $"Failed to save app.pr: {ex.Message}", "IOError", 500));
        }

        return Data.Ok(newApp);
    }
}
