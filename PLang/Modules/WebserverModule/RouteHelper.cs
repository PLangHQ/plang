using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;

namespace PLang.Modules.WebserverModule;

public static class RouteHelper
{
    public static string GetGoalPath(IPLangFileSystem fileSystem, MemoryStack memoryStack, ILogger logger, string url,
        List<GoalRouting> routings)
    {
        if (string.IsNullOrEmpty(url)) return "";

        var goalName = url.AdjustPathToOs().RemoveExtension();
        if (goalName.StartsWith(Path.DirectorySeparatorChar)) goalName = goalName.Substring(1);
        var goalBuildDirPath = Path.Join(fileSystem.BuildPath, goalName).AdjustPathToOs();

        if (!fileSystem.Directory.Exists(goalBuildDirPath))
        {
            logger.LogDebug($"Path doesnt exists - goalBuildDirPath:{goalBuildDirPath}");
            return "";
        }

        // 
        foreach (var route in routings)
        {
            if (route.Path.Contains("%"))
            {
                var input = "/category/Sport";
                var pattern = @"/category/(?<name>.+)";
            }

            if (Regex.IsMatch(url, route.Path)) return goalBuildDirPath;
        }

        return "";
    }
}