using System.Text.Json;
using PLang.Runtime2.Engine.Memory;
using Goal = PLang.Runtime2.Engine.Goals.Goal.@this;

namespace PLang.Runtime2.modules.builder;

/// <summary>
/// Parses .goal files from a path, excludes system goals, merges existing .pr data.
/// </summary>
[Action("getGoals")]
public partial class getGoals : IContext
{
    public partial string Path { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine;
        if (!engine.Building.IsEnabled)
            return Data.FromError(new Engine.Errors.ActionError("Building is not enabled", "BuildingDisabled", 400));

        var fs = engine.FileSystem;
        var searchPath = string.IsNullOrWhiteSpace(Path) ? engine.AbsolutePath : Path;

        // Find .goal files
        List<string> goalFiles;
        try
        {
            var absPath = fs.Path.GetFullPath(
                fs.Path.Combine(engine.AbsolutePath, searchPath));

            if (fs.File.Exists(absPath) && absPath.EndsWith(".goal", StringComparison.OrdinalIgnoreCase))
            {
                goalFiles = new List<string> { absPath };
            }
            else if (fs.Directory.Exists(absPath))
            {
                goalFiles = fs.Directory
                    .GetFiles(absPath, "*.goal", SearchOption.AllDirectories)
                    .ToList();
            }
            else
            {
                return Data.Ok(new List<Goal>());
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new Engine.Errors.ActionError(
                $"Failed to list .goal files: {ex.Message}", "IOError", 500));
        }

        var parser = new GoalFile();
        var allGoals = new List<Goal>();

        foreach (var file in goalFiles)
        {
            string text;
            try
            {
                text = fs.File.ReadAllText(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue; // Skip unreadable files
            }

            // Compute relative path from engine root
            var relativePath = file;
            if (file.StartsWith(engine.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = file[engine.AbsolutePath.Length..];
                if (!relativePath.StartsWith('/') && !relativePath.StartsWith('\\'))
                    relativePath = "/" + relativePath;
            }

            var goals = parser.Parse(text, relativePath);

            // Filter out system goals
            goals = goals.Where(g =>
                g.Path == null ||
                !g.Path.Replace('\\', '/').StartsWith("/system/", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Merge existing .pr data
            foreach (var goal in goals)
                MergePrData(goal, fs);

            allGoals.AddRange(goals);
        }

        return Data.Ok(allGoals);
    }

    private static void MergePrData(Goal goal, Interfaces.IPLangFileSystem fs)
    {
        var prPath = goal.PrPath;
        if (string.IsNullOrEmpty(prPath)) return;

        // Resolve to absolute path — PrPath is relative
        string absPrPath;
        try
        {
            absPrPath = fs.Path.GetFullPath(
                fs.Path.Combine(fs.RootDirectory, prPath.TrimStart('/', '\\')));
        }
        catch
        {
            return;
        }

        if (!fs.File.Exists(absPrPath)) return;

        try
        {
            var prJson = fs.File.ReadAllText(absPrPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // v0.2 format: List<Goal>
            var prGoals = JsonSerializer.Deserialize<List<Goal>>(prJson, options);
            if (prGoals != null)
            {
                var match = prGoals.FirstOrDefault(g =>
                    g.Name.Equals(goal.Name, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    goal.MergeFrom(match);
                return;
            }
        }
        catch (JsonException)
        {
            // Corrupt .pr file — try single goal format
        }

        try
        {
            var prJson = fs.File.ReadAllText(absPrPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var singleGoal = JsonSerializer.Deserialize<Goal>(prJson, options);
            if (singleGoal != null)
                goal.MergeFrom(singleGoal);
        }
        catch (JsonException)
        {
            // Corrupt .pr file — ignore, LLM will rebuild
        }
    }
}
