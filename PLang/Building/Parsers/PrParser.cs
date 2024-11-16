using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.SafeFileSystem;
using PLang.Utils;

namespace PLang.Building.Parsers;

public class PrParser
{
    private static readonly object _lock = new();
    private readonly List<Goal> allGoals = new();
    private readonly IPLangFileSystem fileSystem;
    private readonly Dictionary<string, Instruction> instructions = new();
    private readonly List<Goal> publicGoals = new();

    public PrParser(IPLangFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
    }


    public virtual Goal? ParsePrFile(string absolutePrFilePath)
    {
        var goal = JsonHelper.ParseFilePath<Goal>(fileSystem, absolutePrFilePath);
        if (goal == null) return null;
        var appAbsoluteStartupPath = fileSystem.RootDirectory;
        if (!absolutePrFilePath.StartsWith(fileSystem.RootDirectory))
            appAbsoluteStartupPath = absolutePrFilePath.Substring(0, absolutePrFilePath.IndexOf(".build"));

        var appsPath = absolutePrFilePath.Replace(appAbsoluteStartupPath, "");
        if (appsPath.StartsWith(
                fileSystem.Path.DirectorySeparatorChar + "apps" + fileSystem.Path.DirectorySeparatorChar))
        {
            var paths = appsPath.Split(fileSystem.Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            appsPath = fileSystem.Path.DirectorySeparatorChar + paths[0] + fileSystem.Path.DirectorySeparatorChar +
                       paths[1];
            goal.AppName = paths[1];

            goal.RelativeAppStartupFolderPath = appsPath;
            goal.RelativeGoalFolderPath =
                fileSystem.Path.TrimEndingDirectorySeparator(
                    fileSystem.Path.Join(appsPath, goal.RelativeGoalFolderPath));
            goal.RelativeGoalPath =
                fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, goal.RelativeGoalPath));
            goal.RelativePrPath =
                fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, goal.RelativePrPath));
            goal.RelativePrFolderPath =
                fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, goal.RelativePrFolderPath));
            goal.AbsoluteAppStartupFolderPath =
                fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appAbsoluteStartupPath, appsPath));
        }
        else if (appsPath.StartsWith(fileSystem.Path.DirectorySeparatorChar + ".services" +
                                     fileSystem.Path.DirectorySeparatorChar))
        {
            var i = 0;
            var paths = appsPath.Split(fileSystem.Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            appsPath = fileSystem.Path.DirectorySeparatorChar + paths[0] + fileSystem.Path.DirectorySeparatorChar +
                       paths[1];
            goal.AppName = paths[1];

            goal.RelativeAppStartupFolderPath = appsPath;
            goal.RelativeGoalFolderPath =
                fileSystem.Path.TrimEndingDirectorySeparator(
                    fileSystem.Path.Join(appsPath, goal.RelativeGoalFolderPath));
            goal.RelativeGoalPath =
                fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, goal.RelativeGoalPath));
            goal.RelativePrPath =
                fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, goal.RelativePrPath));
            goal.RelativePrFolderPath =
                fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, goal.RelativePrFolderPath));
            goal.AbsoluteAppStartupFolderPath =
                fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appAbsoluteStartupPath, appsPath));
        }
        else
        {
            goal.AppName = fileSystem.Path.DirectorySeparatorChar.ToString();

            goal.AbsoluteAppStartupFolderPath = appAbsoluteStartupPath;
            goal.RelativeAppStartupFolderPath = fileSystem.Path.DirectorySeparatorChar.ToString();
        }


        goal.AbsoluteGoalPath = fileSystem.Path.Join(appAbsoluteStartupPath, goal.RelativeGoalPath);
        goal.AbsoluteGoalFolderPath = fileSystem.Path.Join(appAbsoluteStartupPath, goal.RelativeGoalFolderPath);

        goal.AbsolutePrFilePath = fileSystem.Path.Join(appAbsoluteStartupPath, goal.RelativePrPath);
        goal.AbsolutePrFolderPath = fileSystem.Path.Join(appAbsoluteStartupPath, goal.RelativePrFolderPath);

        AdjustPathsToOS(goal);

        //var setupOnceDictionary = settings.GetOrDefault<Dictionary<string, DateTime>>(typeof(Engine), "SetupRunOnce", new());
        for (var i = 0; i < goal.GoalSteps.Count; i++)
        {
            goal.GoalSteps[i].AbsolutePrFilePath = fileSystem.Path
                .Join(goal.AbsolutePrFolderPath, goal.GoalSteps[i].PrFileName).AdjustPathToOs();
            goal.GoalSteps[i].RelativePrPath = fileSystem.Path
                .Join(goal.RelativePrFolderPath, goal.GoalSteps[i].PrFileName).AdjustPathToOs();
            goal.GoalSteps[i].AppStartupPath = appAbsoluteStartupPath.AdjustPathToOs();
            goal.GoalSteps[i].Number = i;
            goal.GoalSteps[i].Index = i;
            /*if (setupOnceDictionary != null && setupOnceDictionary.ContainsKey(goal.GoalSteps[i].RelativePrPath))
            {
                goal.GoalSteps[i].Executed = setupOnceDictionary[goal.GoalSteps[i].RelativePrPath];
            }*/
            goal.GoalSteps[i].Goal = goal;
        }

        return goal;
    }

    private static void AdjustPathsToOS(Goal goal)
    {
        goal.RelativeAppStartupFolderPath = goal.RelativeAppStartupFolderPath.AdjustPathToOs();
        goal.RelativeGoalFolderPath = goal.RelativeGoalFolderPath.AdjustPathToOs();
        goal.RelativeGoalPath = goal.RelativeGoalPath.AdjustPathToOs();
        goal.RelativePrPath = goal.RelativePrPath.AdjustPathToOs();
        goal.RelativePrFolderPath = goal.RelativePrFolderPath.AdjustPathToOs();

        goal.AbsoluteAppStartupFolderPath = goal.AbsoluteAppStartupFolderPath.AdjustPathToOs();
        goal.AbsoluteGoalPath = goal.AbsoluteGoalPath.AdjustPathToOs();
        goal.AbsoluteGoalFolderPath = goal.AbsoluteGoalFolderPath.AdjustPathToOs();
        goal.AbsolutePrFilePath = goal.AbsolutePrFilePath.AdjustPathToOs();
        goal.AbsolutePrFolderPath = goal.AbsolutePrFolderPath.AdjustPathToOs();
    }

    public Instruction? ParseInstructionFile(GoalStep step)
    {
        if (!fileSystem.File.Exists(step.AbsolutePrFilePath)) return null;

        if (instructions.TryGetValue(step.AbsolutePrFilePath, out var instruction)) return instruction;

        instruction = JsonHelper.ParseFilePath<Instruction>(fileSystem, step.AbsolutePrFilePath);
        if (instruction != null) return instruction;

        throw new Exception("Could not parse Instruction file.");
    }

    public List<Goal> ForceLoadAllGoals()
    {
        return LoadAllGoals(true);
    }

    public async Task<List<Goal>> GoalFromGoalsFolder(string appName, IFileAccessHandler fileAccessHandler)
    {
        var path = AppContext.BaseDirectory;
        await fileAccessHandler.ValidatePathResponse(appName, path, "y");
        // not using IPlangFileSystem here, we need to get the goal in the runtime folder
        var files = fileSystem.Directory.GetFiles(fileSystem.Path.Join(path, "Goals", ".build"), ISettings.GoalFileName,
            SearchOption.AllDirectories).ToList();

        var goals = new List<Goal>();
        foreach (var file in files)
        {
            var goal = ParsePrFile(file);
            if (goal != null)
            {
                if (allGoals.FirstOrDefault(p => p.RelativePrPath == goal.RelativePrPath) == null) allGoals.Add(goal);
                if (goal.Visibility == Visibility.Public &&
                    publicGoals.FirstOrDefault(p => p.RelativePrPath == goal.RelativePrPath) == null)
                    publicGoals.Add(goal);
            }
        }

        return allGoals;
    }

    public List<Goal> LoadAllGoals(bool force = false)
    {
        if (allGoals.Count > 0 && !force) return allGoals;

        if (!fileSystem.Directory.Exists(fileSystem.Path.Join(fileSystem.RootDirectory, ".build")))
            return new List<Goal>();

        var files = fileSystem.Directory.GetFiles(fileSystem.Path.Join(fileSystem.RootDirectory, ".build"),
            ISettings.GoalFileName, SearchOption.AllDirectories).ToList();

        files = files.Select(file => new
            {
                FileName = file,
                Order = file.ToLower().EndsWith(@"events\events\00. goal.pr") ? 0 :
                    file.ToLower().Contains(@"events\") ? 1 :
                    file.ToLower().Contains(@"setup\") ? 2 :
                    file.ToLower().Contains(@"start\") ? 3 : 4
            }).OrderBy(file => file.Order)
            .ThenBy(file => file.FileName)
            .Select(file => file.FileName).ToList();
        if (fileSystem.Directory.Exists(fileSystem.Path.Join(fileSystem.RootDirectory, "apps")))
        {
            var unsortedFiles = fileSystem.Directory.GetFiles(fileSystem.Path.Join(fileSystem.RootDirectory, "apps"),
                ISettings.GoalFileName, SearchOption.AllDirectories).ToList();
            unsortedFiles = unsortedFiles.Select(file => new
                {
                    FileName = file,
                    Order = file.ToLower().EndsWith(@"events\events\00. goal.pr") ? 0 :
                        file.ToLower().Contains(@"events\") ? 1 :
                        file.ToLower().Contains(@"setup\") ? 2 :
                        file.ToLower().Contains(@"start\") ? 3 : 4
                })
                .OrderBy(file => file.Order)
                .ThenBy(file => file.FileName)
                .Select(file => file.FileName).ToList();
            files.AddRange(unsortedFiles);
        }

        var goals = new List<Goal>();
        foreach (var file in files)
        {
            var goal = ParsePrFile(file);
            if (goal != null) goals.Add(goal);
        }

        var pubGoals = goals.Where(p => p.Visibility == Visibility.Public).ToList();

        // this reloads the whole app
        lock (_lock)
        {
            allGoals.Clear();
            allGoals.AddRange(goals);
            publicGoals.Clear();
            publicGoals.AddRange(pubGoals);
        }

        return allGoals;
    }

    public List<Goal> GetAllGoals()
    {
        if (allGoals.Count > 0) return allGoals;

        LoadAllGoals();
        return allGoals;
    }

    public List<Goal> GetPublicGoals()
    {
        if (publicGoals.Count > 0) return publicGoals;
        LoadAllGoals();
        return publicGoals;
    }

    public Goal? GetGoal(string absolutePrFilePath)
    {
        return ParsePrFile(absolutePrFilePath);
        /*
        if (publicGoals.Count == 0)
        {
            LoadAllGoals();
        }
        return publicGoals.FirstOrDefault(p => p.AbsolutePrFilePath == absolutePrFilePath);
        */
    }

    public Goal? GetGoalByAppAndGoalName(string appStartupPath, string goalNameOrPath, Goal? callingGoal = null)
    {
        /*
         * ProcessVideo - goal belonging to same appStartupPath, located in any folder, root first, then by alphabetical order of folders
         * ui/List - in ui folder,
         * apps/HelloWorld - finds a goal located in apps named HelloWorld
         * apps/Ffmpeg/Convert - maps to apps/Ffmpeg/Convert.goal
         * if you want to use app, path must start with apps/
         */
        appStartupPath = appStartupPath.AdjustPathToOs();
        if (appStartupPath == fileSystem.Path.DirectorySeparatorChar.ToString())
            appStartupPath = fileSystem.RootDirectory;
        goalNameOrPath = goalNameOrPath.AdjustPathToOs().Replace(".goal", "").Replace("!", "");

        if (appStartupPath != fileSystem.RootDirectory)
        {
            appStartupPath = appStartupPath.TrimEnd(fileSystem.Path.DirectorySeparatorChar);
            if (!appStartupPath.StartsWith(fileSystem.Path.DirectorySeparatorChar.ToString()))
                appStartupPath = fileSystem.Path.DirectorySeparatorChar + appStartupPath;
        }

        var goals = GetAllGoals();

        Goal? goal = null;

        // first check for goal inside same goal file as the calling goal
        if (callingGoal != null && !goalNameOrPath.Contains(fileSystem.Path.DirectorySeparatorChar))
        {
            goal = goals.FirstOrDefault(p =>
                p.RelativeGoalFolderPath == callingGoal.RelativeGoalFolderPath &&
                p.GoalName.Equals(goalNameOrPath, StringComparison.OrdinalIgnoreCase));
            if (goal != null) return goal;
        }

        // match goal from root, e.g. /Start
        if (goalNameOrPath.StartsWith(fileSystem.Path.DirectorySeparatorChar))
        {
            goal = goals.FirstOrDefault(p =>
                p.RelativePrFolderPath.Equals(fileSystem.Path.Join(".build", goalNameOrPath),
                    StringComparison.OrdinalIgnoreCase));
            if (goal != null) return goal;
        }

        // match goal from calling goal, e.g. calling goal is in /ui/ folder, when goalNameOrPath is user/edit, it matches /ui/user/edit.goal
        if (callingGoal != null && !goalNameOrPath.StartsWith(fileSystem.Path.DirectorySeparatorChar))
        {
            var newGoalPath = fileSystem.Path.Join(".build", callingGoal.RelativeGoalFolderPath, goalNameOrPath);
            goal = goals.FirstOrDefault(p =>
                p.RelativePrFolderPath.Equals(newGoalPath, StringComparison.OrdinalIgnoreCase));
            if (goal != null) return goal;
        }

        goal = goals.FirstOrDefault(p =>
            p.RelativePrFolderPath.Equals(fileSystem.Path.Join(".build", goalNameOrPath),
                StringComparison.OrdinalIgnoreCase));
        if (goal != null) return goal;

        goal = goals.FirstOrDefault(p =>
            goalNameOrPath.TrimStart(fileSystem.Path.DirectorySeparatorChar).Equals(
                fileSystem.Path.Join(p.RelativeGoalFolderPath, p.GoalName)
                    .TrimStart(fileSystem.Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase));
        if (goal != null) return goal;


        var possibleGoals = goals
            .Where(p => p.RelativePrFolderPath.EndsWith(goalNameOrPath, StringComparison.OrdinalIgnoreCase)).ToList();
        if (possibleGoals.Count == 1) return possibleGoals[0];
        if (possibleGoals.Count > 1)
        {
            var goalNames = possibleGoals.Select(p => { return p.RelativeGoalPath; });
            throw new GoalNotFoundException(
                $"{goalNameOrPath} Could not be found. There are {possibleGoals.Count} to choose from. {string.Join(",", goalNames)}",
                appStartupPath, goalNameOrPath);
        }

        return goal;
    }


    public List<Goal> GetApps()
    {
        var groupedGoals = GetAllGoals().GroupBy(p => p.AppName);
        var goals = new List<Goal>();
        foreach (var groupedGoal in groupedGoals)
        {
            var goal = groupedGoal.FirstOrDefault();
            if (goal != null &&
                goal.RelativeAppStartupFolderPath.StartsWith(fileSystem.Path.DirectorySeparatorChar + "apps"))
                goals.Add(goal);
        }

        return goals;
    }
}