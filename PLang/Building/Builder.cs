using System.Diagnostics;
using LightInject;
using Microsoft.Extensions.Logging;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Errors.Handlers;
using PLang.Events;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.SafeFileSystem;
using PLang.Utils;
using AskUserError = PLang.Errors.AskUser.AskUserError;

namespace PLang.Building;

public interface IBuilder
{
    Task<IError?> Start(IServiceContainer container);
}

public class Builder : IBuilder
{
    private readonly IAskUserHandlerFactory askUserHandlerFactory;
    private readonly IEventBuilder eventBuilder;
    private readonly IEventRuntime eventRuntime;
    private readonly IErrorHandlerFactory exceptionHandlerFactory;
    private readonly IPLangFileSystem fileSystem;
    private readonly IGoalBuilder goalBuilder;
    private readonly ILogger logger;
    private readonly PrParser prParser;
    private readonly ISettings settings;

    public Builder(ILogger logger, IPLangFileSystem fileSystem, ISettings settings, IGoalBuilder goalBuilder,
        IEventBuilder eventBuilder, IEventRuntime eventRuntime,
        PrParser prParser, IErrorHandlerFactory exceptionHandlerFactory, IAskUserHandlerFactory askUserHandlerFactory)
    {
        this.fileSystem = fileSystem;
        this.logger = logger;
        this.settings = settings;
        this.goalBuilder = goalBuilder;
        this.eventBuilder = eventBuilder;
        this.eventRuntime = eventRuntime;
        this.prParser = prParser;
        this.exceptionHandlerFactory = exceptionHandlerFactory;
        this.askUserHandlerFactory = askUserHandlerFactory;
    }


    public async Task<IError?> Start(IServiceContainer container)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            AppContext.SetSwitch("Builder", true);

            SetupBuildValidation();

            var goalFiles = GoalHelper.GetGoalFilesToBuild(fileSystem, fileSystem.GoalsPath);

            InitFolders();
            logger.LogInformation("Build Start:" + DateTime.Now.ToLongTimeString());

            var (eventGoalFiles, error) = await eventBuilder.BuildEventsPr();
            if (error != null) return error;

            error = await eventRuntime.Load(true);
            if (error != null) return error;

            var eventError = await eventRuntime.RunStartEndEvents(new PLangAppContext(), EventType.Before,
                EventScope.StartOfApp, true);
            if (eventError != null)
            {
                if (!eventError.IgnoreError) return eventError;

                logger.LogError(eventError.ToString());
            }

            foreach (var file in goalFiles)
            {
                var goalError = await goalBuilder.BuildGoal(container, file);
                if (goalError != null && !goalError.ContinueBuild) return goalError;

                if (goalError != null) logger.LogWarning(goalError.ToFormat().ToString());
            }

            goalFiles.AddRange(eventGoalFiles);
            CleanGoalFiles(goalFiles);

            eventError =
                await eventRuntime.RunStartEndEvents(new PLangAppContext(), EventType.After, EventScope.EndOfApp, true);
            if (eventError != null && !eventError.IgnoreError) return eventError;

            if (eventError != null) logger.LogWarning(eventError.ToFormat().ToString());

            ShowBuilderErrors(goalFiles, stopwatch);
        }
        catch (Exception ex)
        {
            if (ex is FileAccessException fa)
            {
                var fileAccessHandler = container.GetInstance<IFileAccessHandler>();
                var askUserFileAccess = new AskUserFileAccess(fa.AppName, fa.Path, fa.Message,
                    fileAccessHandler.ValidatePathResponse);

                var (isFaHandled, handlerError) = await askUserHandlerFactory.CreateHandler().Handle(askUserFileAccess);
                if (isFaHandled) return await Start(container);

                return ErrorHelper.GetMultipleError(askUserFileAccess, handlerError);
            }

            if (ex is MissingSettingsException mse)
            {
                var settingsError = new AskUserError(mse.Message, async result =>
                {
                    var value = result?[0] ?? null;
                    if (value is Array) value = ((object[])value)[0];

                    await mse.InvokeCallback(value);
                    return (true, null);
                });

                var (isMseHandled, handlerError) = await askUserHandlerFactory.CreateHandler().Handle(settingsError);
                if (isMseHandled) return await Start(container);
            }


            var error = new ExceptionError(ex);
            var handler = exceptionHandlerFactory.CreateHandler();
            var (isHandled, handleError) = await handler.Handle(error);
            if (!isHandled)
            {
                if (handleError != null)
                {
                    var me = new MultipleError(error);
                    me.Add(handleError);
                    await handler.ShowError(error);
                }
                else
                {
                    await handler.ShowError(error);
                }
            }
        }

        return null;
    }

    private void ShowBuilderErrors(List<string> goalFiles, Stopwatch stopwatch)
    {
        if (goalBuilder.BuildErrors.Count > 0)
        {
            foreach (var buildError in goalBuilder.BuildErrors) logger.LogWarning(buildError.ToFormat().ToString());

            logger.LogError($"\n\n❌ Failed to build {goalBuilder.BuildErrors.Count} steps");
        }
        else
        {
            logger.LogWarning("\n\n\ud83c\udf89 Build was succesfull!");
        }

        if (goalFiles.Count == 0)
            logger.LogInformation(
                $"No goal files changed since last build - Time:{stopwatch.Elapsed.TotalSeconds.ToString("#,##.##")} sec - at {DateTime.Now}");
        else
            logger.LogInformation(
                $"Build done - Time:{stopwatch.Elapsed.TotalSeconds.ToString("#,##.##")} sec - started at {DateTime.Now}");
    }

    private void InitFolders()
    {
        var buildPath = fileSystem.Path.Join(fileSystem.RootDirectory, ".build");
        if (!fileSystem.Directory.Exists(buildPath))
        {
            var dir = fileSystem.Directory.CreateDirectory(buildPath);
            dir.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
        }

        var dbPath = fileSystem.Path.Join(fileSystem.RootDirectory, ".db");
        if (!fileSystem.Directory.Exists(dbPath))
        {
            var dir = fileSystem.Directory.CreateDirectory(dbPath);
            dir.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
        }
    }

    private void CleanGoalFiles(List<string> goalFiles)
    {
        var dirs = fileSystem.Directory.GetDirectories(".build", "", SearchOption.AllDirectories);

        foreach (var goalFile in goalFiles)
        {
            var buildFolderRelativePath = fileSystem.Path.Join(".build", goalFile.Replace(fileSystem.RootDirectory, ""))
                .Replace(".goal", "");
            var buildFolderAbsolutePath = fileSystem.Path.Join(fileSystem.RootDirectory, buildFolderRelativePath);

            dirs = dirs.Where(p => !p.Equals(buildFolderAbsolutePath, StringComparison.OrdinalIgnoreCase)).ToArray();
            dirs = dirs.Where(p =>
                !p.StartsWith(
                    fileSystem.Path.Join(buildFolderAbsolutePath, fileSystem.Path.DirectorySeparatorChar.ToString()),
                    StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        foreach (var dir in dirs)
        {
            var folderPath = fileSystem.Path.Join(fileSystem.RootDirectory, dir.Replace(fileSystem.BuildPath, ""));
            if (!fileSystem.Directory.Exists(folderPath) && fileSystem.Directory.Exists(dir))
                fileSystem.Directory.Delete(dir, true);
        }

        var prGoalFiles = prParser.ForceLoadAllGoals();
        var i = 0;

        /*

        var prGoalFiles = prParser.ForceLoadAllGoals();
        foreach (var dir in dirs)
        {
            var matchingGoal = prGoalFiles.FirstOrDefault(p => p.AbsolutePrFolderPath.ToLower().StartsWith(dir.ToLower()));
            if (matchingGoal == null && fileSystem.Directory.Exists(dir))
            {
                fileSystem.Directory.Delete(dir, true);
            }

            string goalFolder = dir.Replace(fileSystem.BuildPath, "");
            string goalFolderPath = Path.Join(fileSystem.RootDirectory, goalFolder);
            string goalFileName = dir.Replace(fileSystem.BuildPath, "") + ".goal";
            string goalFilePath = Path.Join(fileSystem.RootDirectory, goalFileName);

            if (!fileSystem.File.Exists(goalFilePath) && !fileSystem.Directory.Exists(goalFolderPath))
            {
                fileSystem.Directory.Delete(dir, true);
            }

        }*/
    }


    public void SetupBuildValidation()
    {
        /*
        var eventsPath = fileSystem.Path.Join(fileSystem.GoalsPath, "events", "external", "plang", "builder");

        if (fileSystem.Directory.Exists(eventsPath)) return;

        fileSystem.Directory.CreateDirectory(eventsPath);

        using (MemoryStream ms = new MemoryStream(InternalApps.Builder))
        using (ZipArchive archive = new ZipArchive(ms))
        {
            archive.ExtractToDirectory(fileSystem.GoalsPath, true);
        }
        return;
        */
    }
}