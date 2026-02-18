using System.Text.Json;
using System.Text.Json.Serialization;
using PLang.Runtime2.Engine;
using Goal = PLang.Runtime2.Engine.Goals.Goal.@this;

namespace PLang.Runtime2.Engine.Utility;

/// <summary>
/// Parser for Runtime2 v0.2 .pr.json files.
/// </summary>
public class PrParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Parse a v0.2 .pr.json file to a Runtime2 Goal object.
    /// </summary>
    /// <param name="prFilePath">Absolute path to the .pr.json file</param>
    /// <returns>Parsed Goal or null if file doesn't exist or is invalid</returns>
    public Goal? ParsePrFile(string prFilePath)
    {
        if (string.IsNullOrEmpty(prFilePath))
            return null;

        if (!File.Exists(prFilePath))
            return null;

        try
        {
            var json = File.ReadAllText(prFilePath);
            var goal = JsonSerializer.Deserialize<Goal>(json, JsonOptions);

            if (goal == null)
                return null;

            goal.Path = ExtractRelativePath(prFilePath);
            foreach (var step in goal.Steps) step.Goal = goal;

            return goal;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Get all goals from .pr.json files in the given root path.
    /// </summary>
    /// <param name="rootPath">Root directory to search</param>
    /// <returns>List of parsed Goal objects</returns>
    public List<Goal> GetAllGoals(string rootPath)
    {
        var goals = new List<Goal>();

        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            return goals;

        var buildPath = Path.Combine(rootPath, ".build");
        if (!Directory.Exists(buildPath))
            return goals;

        // Find all .pr.json files (v0.2 format) and .pr files (v0.1 format)
        var prFiles = Directory.GetFiles(buildPath, "*.pr.json", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(buildPath, "*.pr", SearchOption.AllDirectories))
            .Where(f => !f.EndsWith(".pr.json.pr")) // Avoid double extension matches
            .OrderBy(f => GetFileOrder(f))
            .ThenBy(f => f);

        foreach (var file in prFiles)
        {
            var goal = ParsePrFile(file);
            if (goal != null)
            {
                goals.Add(goal);
            }
        }

        return goals;
    }

    /// <summary>
    /// Load the app.pr file for the application.
    /// </summary>
    /// <param name="rootPath">Root path of the application</param>
    /// <returns>AppData or null</returns>
    public AppData? LoadAppData(string rootPath)
    {
        var appPrPath = Path.Combine(rootPath, ".build", "app.pr");

        if (!File.Exists(appPrPath))
            return null;

        try
        {
            var json = File.ReadAllText(appPrPath);
            return JsonSerializer.Deserialize<AppData>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Save app data to the app.pr file.
    /// </summary>
    /// <param name="rootPath">Root path of the application</param>
    /// <param name="appData">App data to save</param>
    public void SaveAppData(string rootPath, AppData appData)
    {
        var buildPath = Path.Combine(rootPath, ".build");
        Directory.CreateDirectory(buildPath);

        var appPrPath = Path.Combine(buildPath, "app.pr");
        var json = JsonSerializer.Serialize(appData, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        File.WriteAllText(appPrPath, json);
    }

    private static string? ExtractRelativePath(string prFilePath)
    {
        var buildIndex = prFilePath.IndexOf(".build", StringComparison.OrdinalIgnoreCase);
        if (buildIndex < 0)
            return null;

        return prFilePath.Substring(buildIndex);
    }

    private static string? ConvertPrPathToGoalPath(string prFilePath)
    {
        // Convert .build/SomeGoal/00. Goal.pr to SomeGoal.goal
        var buildIndex = prFilePath.IndexOf(".build", StringComparison.OrdinalIgnoreCase);
        if (buildIndex < 0)
            return null;

        var rootPath = prFilePath.Substring(0, buildIndex);
        var afterBuild = prFilePath.Substring(buildIndex + 7); // Skip ".build/"

        // Remove the filename (00. Goal.pr or similar)
        var dirPath = Path.GetDirectoryName(afterBuild);
        if (string.IsNullOrEmpty(dirPath))
            return null;

        return Path.Combine(rootPath, dirPath + ".goal");
    }

    private static int GetFileOrder(string filePath)
    {
        var lowerPath = filePath.ToLowerInvariant();

        if (lowerPath.EndsWith("events" + Path.DirectorySeparatorChar + "00. goal.pr") ||
            lowerPath.EndsWith("events" + Path.DirectorySeparatorChar + "00. goal.pr.json"))
            return 0;

        if (lowerPath.Contains(Path.DirectorySeparatorChar + "events" + Path.DirectorySeparatorChar))
            return 1;

        if (lowerPath.Contains(Path.DirectorySeparatorChar + "setup" + Path.DirectorySeparatorChar))
            return 2;

        if (lowerPath.Contains(Path.DirectorySeparatorChar + "start" + Path.DirectorySeparatorChar))
            return 3;

        return 4;
    }
}
