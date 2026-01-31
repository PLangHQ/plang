using PLang.Building.Model;
using PLang.Errors;
using PLang.Models;
using PLang.SafeFileSystem;

namespace PLang.Building.Parsers;

public interface IPrParser
{
    Goal? GetEvent(string name);
    List<Goal> GetEvents(string name);
    Goal? GetSystemEvent(string name);
    List<Goal> GetSystemEvents(string name);
    Goal? ParsePrFile(string absolutePrFilePath);
    Instruction? ParseInstructionFile(GoalStep step);
    IReadOnlyList<Goal> ForceLoadAllGoals();
    IReadOnlyList<Goal> LoadAllGoals(bool force = false);
    List<Goal> LoadAllGoalsByPath(string dir);
    List<Goal> LoadAppsByPath(string dir);
    IReadOnlyList<Goal> GetAllGoals();
    IReadOnlyList<Goal> GetPublicGoals();
    (Goal? Goal, IError? Error) GetGoal(GoalToCallInfo goalToCall);
    Goal? GetGoal(string absolutePrFilePath);
    Goal? GetGoalByAppAndGoalName(string appStartupPath, string goalNameOrPath, Goal? callingGoal = null);
    IReadOnlyList<Goal> GetGoals(bool force = false);
    IReadOnlyList<Goal> GetSystemGoals(bool force = false);
    List<Goal> GetApps();
    Task<(List<Goal>? Goals, IError? Error)> LoadAppPath(string appName, IFileAccessHandler fileAccessHandler);
    (List<Instruction>? Instructions, IError? Error) GetInstructions(IReadOnlyList<GoalStep> steps, string? functionName = null);
    IReadOnlyList<Goal> GetEventsFiles(bool builder = false);
}
