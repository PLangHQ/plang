using System.Text.Json;
using PLang.Runtime2.Engine.Memory;
using Goal = PLang.Runtime2.Engine.Goals.Goal.@this;

namespace PLang.Runtime2.modules.builder;

/// <summary>
/// Parses .goal files from a path, marks system goals, merges existing .pr data.
/// Hash is lazy-computed by Goal itself.
/// </summary>
[Action("goals")]
public partial class goals : IContext
{
    public partial string Path { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine;
        if (!engine.Building.IsEnabled)
            return Data.FromError(new Engine.Errors.ActionError("Building is not enabled", "BuildingDisabled", 400));

        var searchPath = string.IsNullOrWhiteSpace(Path) ? "." : Path;

        // List .goal files via file.list action
        var listAction = new file.List
        {
            Context = Context,
            Path = new PLangPath(searchPath, Context),
            Pattern = "*.goal",
            Recursive = true
        };
        var listResult = await engine.RunAction(listAction, Context);
        if (!listResult.Success)
            return listResult;

        var files = listResult.Value as PLangPath[];
        if (files == null || files.Length == 0)
            return Data.Ok(new List<Goal>());

        var parser = new GoalFile();
        var allGoals = new List<Goal>();

        foreach (var file in files)
        {
            // Read file content via file.read action
            var readAction = new file.Read
            {
                Context = Context,
                Path = file
            };
            var readResult = await engine.RunAction(readAction, Context);
            if (!readResult.Success)
                continue; // Skip unreadable files

            var text = readResult.Value switch
            {
                string s => s,
                byte[] b => System.Text.Encoding.UTF8.GetString(b),
                _ => readResult.Value?.ToString()
            };
            if (string.IsNullOrWhiteSpace(text))
                continue;

            // Compute relative path from engine root
            var relativePath = file.Relative ?? file.Raw;
            if (!relativePath.StartsWith('/') && !relativePath.StartsWith('\\'))
                relativePath = "/" + relativePath;

            var parsedGoals = parser.Parse(text, relativePath);

            // Mark system goals instead of filtering
            var normalizedPath = relativePath.Replace('\\', '/');
            if (normalizedPath.StartsWith("/system/", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var goal in parsedGoals)
                    goal.IsSystem = true;
            }

            // Merge existing .pr data
            foreach (var goal in parsedGoals)
                await MergePrData(goal, engine);

            allGoals.AddRange(parsedGoals);
        }

        return Data.Ok(allGoals);
    }

    private async Task MergePrData(Goal goal, Engine.@this engine)
    {
        var prPath = goal.PrPath;
        if (string.IsNullOrEmpty(prPath)) return;

        // Read .pr file via file.read action
        var readAction = new file.Read
        {
            Context = Context,
            Path = new PLangPath(prPath, Context)
        };
        var readResult = await engine.RunAction(readAction, Context);
        if (!readResult.Success)
            return; // No .pr file — nothing to merge

        var prJson = readResult.Value switch
        {
            string s => s,
            byte[] b => System.Text.Encoding.UTF8.GetString(b),
            _ => readResult.Value?.ToString()
        };
        if (string.IsNullOrWhiteSpace(prJson)) return;

        // Try v0.2 format: List<Goal>
        try
        {
            var prGoals = JsonSerializer.Deserialize<List<Goal>>(prJson, JsonOptions.CaseInsensitive);
            if (prGoals != null)
            {
                var match = prGoals.FirstOrDefault(g =>
                    g.Name.Equals(goal.Name, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    goal.MergeFrom(match);
                return;
            }
        }
        catch (JsonException ex)
        {
            goal.Errors.Add(new Engine.Info
            {
                Key = "CorruptPrFile",
                Message = $"Failed to parse .pr file at {prPath}: {ex.Message}"
            });
        }
    }
}
