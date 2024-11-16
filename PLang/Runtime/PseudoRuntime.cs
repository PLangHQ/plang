using LightInject;
using PLang.Building.Model;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.Handlers;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Models;
using PLang.Services.OutputStream;
using PLang.Utils;

namespace PLang.Runtime;

public interface IPseudoRuntime
{
    Task<(IEngine engine, IError? error)> RunGoal(IEngine engine, PLangAppContext context, string appPath,
        GoalToCall goalName,
        Dictionary<string, object?>? parameters, Goal? callingGoal = null, bool waitForExecution = true,
        long delayWhenNotWaitingInMilliseconds = 50, uint waitForXMillisecondsBeforeRunningGoal = 0, int indent = 0,
        bool keepMemoryStackOnAsync = false);
}

public class PseudoRuntime : IPseudoRuntime
{
    private readonly IAskUserHandlerFactory askUserHandlerFactory;
    private readonly IErrorHandlerFactory errorHandlerFactory;
    private readonly IErrorSystemHandlerFactory errorSystemHandlerFactory;
    private readonly IPLangFileSystem fileSystem;
    private readonly IOutputStreamFactory outputStreamFactory;
    private readonly IOutputSystemStreamFactory outputSystemStreamFactory;
    private readonly IServiceContainerFactory serviceContainerFactory;

    public PseudoRuntime(IServiceContainerFactory serviceContainerFactory, IPLangFileSystem fileSystem,
        IOutputStreamFactory outputStreamFactory, IOutputSystemStreamFactory outputSystemStreamFactory,
        IErrorHandlerFactory errorHandlerFactory, IErrorSystemHandlerFactory errorSystemHandlerFactory,
        IAskUserHandlerFactory askUserHandlerFactory)
    {
        this.serviceContainerFactory = serviceContainerFactory;
        this.fileSystem = fileSystem;
        this.outputStreamFactory = outputStreamFactory;
        this.outputSystemStreamFactory = outputSystemStreamFactory;
        this.errorHandlerFactory = errorHandlerFactory;
        this.errorSystemHandlerFactory = errorSystemHandlerFactory;
        this.askUserHandlerFactory = askUserHandlerFactory;
    }

    public async Task<(IEngine engine, IError? error)> RunGoal(IEngine engine, PLangAppContext context, string appPath,
        GoalToCall goalName,
        Dictionary<string, object?>? parameters, Goal? callingGoal = null,
        bool waitForExecution = true, long delayWhenNotWaitingInMilliseconds = 50,
        uint waitForXMillisecondsBeforeRunningGoal = 0,
        int indent = 0, bool keepMemoryStackOnAsync = false)
    {
        if (goalName == null || goalName.Value == null)
            return (engine, new Error($"Goal to call is empty. Calling goal is {callingGoal}"));
        Goal? goal = null;
        ServiceContainer? container = null;

        var absolutePathToGoal = Path.Join(fileSystem.RootDirectory, appPath, goalName).AdjustPathToOs();
        string goalToRun = goalName;
        if (CreateNewContainer(absolutePathToGoal))
        {
            var pathAndGoal = GetAppAbsolutePath(absolutePathToGoal);
            var absoluteAppStartupPath = pathAndGoal.absolutePath;
            var relativeAppStartupPath = Path.DirectorySeparatorChar.ToString();
            goalToRun = pathAndGoal.goalName;

            container = serviceContainerFactory.CreateContainer(context, absoluteAppStartupPath, relativeAppStartupPath,
                outputStreamFactory, outputSystemStreamFactory,
                errorHandlerFactory, errorSystemHandlerFactory, askUserHandlerFactory);

            engine = container.GetInstance<IEngine>();
            engine.Init(container);

            if (context.ContainsKey(ReservedKeywords.IsEvent))
                engine.GetContext().AddOrReplace(ReservedKeywords.IsEvent, true);

            goal = engine.GetGoal(goalToRun);
        }
        else
        {
            goal = engine.GetGoal(goalToRun, callingGoal);
        }


        if (goal == null)
        {
            var goalsAvailable = engine.GetGoalsAvailable(appPath, goalToRun);
            if (goalsAvailable == null || goalsAvailable.Count == 0)
                return (engine, new Error($"No goals available at {appPath} trying to run {goalToRun}"));

            var goals = string.Join('\n',
                goalsAvailable.OrderBy(p => p.GoalName).Select(p => $" - {p.GoalName} -> Path:{p.RelativeGoalPath}"));
            var strGoalsAvailable = "";
            if (!string.IsNullOrWhiteSpace(goals)) strGoalsAvailable = $" These goals are available: \n{goals}";
            return (engine,
                new Error(
                    $"WARNING! - Goal '{goalName}' at {fileSystem.RootDirectory} was not found.{strGoalsAvailable}"));
        }

        if (waitForExecution)
        {
            goal.ParentGoal = callingGoal;
        }
        else if (!keepMemoryStackOnAsync)
        {
            var newContext = new PLangAppContext();
            foreach (var item in context) newContext.Add(item.Key, item.Value);
            engine.GetContext().Clear();
            engine.GetContext().AddOrReplace(newContext);
        }

        var memoryStack = engine.GetMemoryStack();
        /*
        var oldMemoryStack = new Dictionary<string, ObjectValue>();
        if (memoryStack != null)
        {
            foreach (var item in memoryStack.GetMemoryStack())
            {
                if (!oldMemoryStack.ContainsKey(item.Key))
                {
                    oldMemoryStack.Add(item.Key, item.Value);
                }
            }
            memoryStack.Clear();
        }*/


        if (parameters != null)
            foreach (var param in parameters)
            {
                var value = param.Value;
                if (VariableHelper.IsVariable(param.Value)) value = memoryStack.Get(param.Value?.ToString());

                memoryStack.Put(param.Key.Replace("%", ""), value);
            }

        var prevIndent = context.GetOrDefault(ReservedKeywords.ParentGoalIndent, 0);
        context.AddOrReplace(ReservedKeywords.ParentGoalIndent, prevIndent + indent);

        var task = engine.RunGoal(goal, waitForXMillisecondsBeforeRunningGoal);
        await task.ConfigureAwait(waitForExecution);
        /*
        if (waitForExecution)
        {
            try
            {
                await task;
            }
            catch { }
        } else if (delayWhenNotWaitingInMilliseconds > 0)
        {
            await Task.Delay((int) delayWhenNotWaitingInMilliseconds);
            if (!waitForExecution)
            {
                context.Remove(ReservedKeywords.IsEvent);
            }
        }*/
        /*
        if (memoryStack != null)
        {
            memoryStack.GetMemoryStack().Clear();
            var internalStack = memoryStack.GetMemoryStack();
            foreach (var item in oldMemoryStack)
            {
                if (!internalStack.ContainsKey(item.Key))
                {
                    internalStack.Add(item.Key, item.Value);
                }
            }
        }
        */

        if (container != null) container.Dispose();
        context.AddOrReplace(ReservedKeywords.ParentGoalIndent, prevIndent);

        if (task.IsFaulted && task.Exception != null)
        {
            var error = new Error(task.Exception.Message, Exception: task.Exception);
            return (engine, error);
        }

        return (engine, task.Result);
    }

    public (string absolutePath, string goalName) GetAppAbsolutePath(string absolutePathToGoal)
    {
        absolutePathToGoal = absolutePathToGoal.AdjustPathToOs();

        Dictionary<string, int> dict = new();
        dict.Add("apps", absolutePathToGoal.LastIndexOf("apps"));
        dict.Add(".modules", absolutePathToGoal.LastIndexOf(".modules"));
        dict.Add(".services", absolutePathToGoal.LastIndexOf(".services"));

        var item = dict.OrderByDescending(p => p.Value).FirstOrDefault();
        if (item.Value == -1) return (absolutePathToGoal, "");

        var idx = absolutePathToGoal.IndexOf(Path.DirectorySeparatorChar, item.Value + item.Key.Length + 1);
        if (idx == -1) idx = absolutePathToGoal.IndexOf(Path.DirectorySeparatorChar, item.Value + item.Key.Length);

        var absolutePathToApp = absolutePathToGoal.Substring(0, idx);
        foreach (var itemInDict in dict)
            if (absolutePathToApp.EndsWith(itemInDict.Key))
                absolutePathToApp = absolutePathToGoal;

        if (absolutePathToApp.EndsWith(Path.DirectorySeparatorChar))
            absolutePathToApp = absolutePathToApp.TrimEnd(Path.DirectorySeparatorChar);

        var goalName = absolutePathToGoal.Replace(absolutePathToApp, "").TrimStart(Path.DirectorySeparatorChar);
        if (string.IsNullOrEmpty(goalName)) goalName = "Start";

        return (absolutePathToApp, goalName);
    }

    private bool CreateNewContainer(string absoluteGoalPath)
    {
        var servicesFolder = Path.Join(fileSystem.RootDirectory, ".services");
        var modulesFolder = Path.Join(fileSystem.RootDirectory, ".modules");
        var appsFolder = Path.Join(fileSystem.RootDirectory, "apps");
        return absoluteGoalPath.StartsWith(servicesFolder) || absoluteGoalPath.StartsWith(modulesFolder) ||
               absoluteGoalPath.StartsWith(appsFolder);
    }
}