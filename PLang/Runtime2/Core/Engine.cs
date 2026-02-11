using PLang.Runtime2.Context;
using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;
using PLang.Runtime2.modules;
using PLang.Runtime2.Serialization;

namespace PLang.Runtime2.Core;

/// <summary>
/// Main runtime engine for PLang Runtime2.
/// Executes goals and manages the execution lifecycle.
/// </summary>
public sealed class Engine : IAsyncDisposable
{
    private readonly PLangAppContext _appContext;
    private readonly ActionRegistry _actions;
    private readonly SerializerRegistry _serializers;
    private readonly Goals _goals;
    private bool _disposed;

    private Actor? _system;
    private Actor? _service;
    private Actor? _user;

    /// <summary>
    /// Unique identifier for this engine instance.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Name of this engine.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Root path of the application.
    /// </summary>
    public string RootPath => _appContext.RootPath;

    /// <summary>
    /// The application context.
    /// </summary>
    public PLangAppContext AppContext => _appContext;

    /// <summary>
    /// The action registry.
    /// </summary>
    public ActionRegistry Actions => _actions;

    /// <summary>
    /// The serializer registry.
    /// </summary>
    public SerializerRegistry Serializers => _serializers;

    /// <summary>
    /// The loaded goals.
    /// </summary>
    public Goals Goals => _goals;

    /// <summary>
    /// The file system abstraction.
    /// </summary>
    public Interfaces.IPLangFileSystem FileSystem { get; set; }

    /// <summary>
    /// I/O operations (file reading, channels).
    /// </summary>
    public Runtime2.IO.IO IO { get; }

    /// <summary>
    /// Whether debug mode is enabled.
    /// </summary>
    public bool IsDebugMode
    {
        get => _appContext.IsDebugMode;
        set => _appContext.IsDebugMode = value;
    }

    /// <summary>
    /// System actor - highest trust level, for app operator operations.
    /// Created lazily on first access.
    /// </summary>
    public Actor System => _system ??= new Actor("System", TrustLevel.System, this);

    /// <summary>
    /// Service actor - intermediate trust level, for external service operations.
    /// Created lazily on first access.
    /// </summary>
    public Actor Service => _service ??= new Actor("Service", TrustLevel.Service, this);

    /// <summary>
    /// User actor - lowest trust level, for end user operations.
    /// Created lazily on first access.
    /// </summary>
    public Actor User => _user ??= new Actor("User", TrustLevel.User, this);

    public PLangContext Context => User.Context;
    public Memory.MemoryStack MemoryStack => User.Context.MemoryStack;

    /// <summary>
    /// Resolves an actor by name. Returns error instead of null — object reports its own errors.
    /// </summary>
    public (Actor? Actor, IError? Error) GetActor(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return (null, new ActionError("Actor name is required", "ActorRequired", 400));

        var actor = name.ToLowerInvariant() switch
        {
            "system" => System,
            "service" => Service,
            "user" => User,
            _ => (Actor?)null
        };

        if (actor == null)
            return (null, new ActionError($"Unknown actor '{name}'", "UnknownActor", 400));

        return (actor, null);
    }

    public Engine(Interfaces.IPLangFileSystem fileSystem)
        : this(new PLangAppContext(fileSystem.RootDirectory), fileSystem: fileSystem)
    {
    }

    public Engine(PLangAppContext appContext, ActionRegistry? actions = null,
        SerializerRegistry? serializers = null, Interfaces.IPLangFileSystem? fileSystem = null)
    {
        Id = Guid.NewGuid().ToString("N")[..12];
        Name = "Runtime2";
        _appContext = appContext;
        _actions = actions ?? new ActionRegistry();
        _serializers = serializers ?? appContext.Serializers;
        _goals = new Goals { Engine = this };
        FileSystem = fileSystem ?? CreateDefaultFileSystem(appContext.RootPath);
        IO = new Runtime2.IO.IO(this);
        RegisterBuiltInModules();
    }

    /// <summary>
    /// Runs a goal by name using the User actor by default.
    /// </summary>
    public Task<Data> RunGoalAsync(string goalName, PLangContext? context = null, CancellationToken cancellationToken = default)
    {
        return _goals.Run(goalName, context, cancellationToken);
    }

    /// <summary>
    /// Runs a goal by name using the specified actor.
    /// </summary>
    public Task<Data> RunGoalAsync(string goalName, Actor actor, CancellationToken cancellationToken = default)
    {
        return RunGoalAsync(goalName, actor.Context, cancellationToken);
    }

    /// <summary>
    /// Runs a goal using the specified actor.
    /// </summary>
    public Task<Data> RunGoalAsync(Goal goal, Actor actor, CancellationToken cancellationToken = default)
    {
        return RunGoalAsync(goal, actor.Context, cancellationToken);
    }

    /// <summary>
    /// Runs a goal using the User actor's context by default.
    /// </summary>
    public async Task<Data> RunGoalAsync(Goal goal, PLangContext? context = null, CancellationToken cancellationToken = default)
    {
        context ??= User.Context;

        var loadResult = await goal.Load(context);
        if (!loadResult.Success) return loadResult;

        return await goal.RunAsync(this, context, cancellationToken);
    }

    /// <summary>
    /// Loads a goal from a .pr file. Delegates to Goals.LoadFromFileAsync.
    /// </summary>
    public Task<Data> LoadGoalFromFileAsync(string prFilePath, CancellationToken cancellationToken = default)
    {
        return _goals.LoadFromFileAsync(this, prFilePath, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Loads all goals from a directory. Delegates to Goals.LoadFromDirectoryAsync.
    /// </summary>
    public Task<Data> LoadGoalsFromDirectoryAsync(string directory, string pattern = "*.pr", CancellationToken cancellationToken = default)
    {
        return _goals.LoadFromDirectoryAsync(this, directory, pattern, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates a new execution context.
    /// </summary>
    public PLangContext CreateContext(MemoryStack? memoryStack = null)
    {
        var context = new PLangContext(_appContext, memoryStack)
        {
            CallStack = new CallStack()
        };
        context.RegisterContextVariables(this);
        return context;
    }

    private static Interfaces.IPLangFileSystem CreateDefaultFileSystem(string rootPath)
    {
        try
        {
            var fullPath = global::System.IO.Path.GetFullPath(rootPath);
            return new SafeFileSystem.PLangFileSystem(fullPath, "");
        }
        catch
        {
            // If rootPath is not a valid filesystem path (e.g., "/app" in tests),
            // fall back to PLangFileSystem with current directory
            return new SafeFileSystem.PLangFileSystem(global::System.IO.Directory.GetCurrentDirectory(), "");
        }
    }

    /// <summary>
    /// Registers built-in modules via reflection discovery.
    /// </summary>
    public void RegisterBuiltInModules()
    {
        _actions.DiscoverAndRegister(typeof(Engine).Assembly);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Dispose created actors
        if (_system != null)
            await _system.DisposeAsync();
        if (_service != null)
            await _service.DisposeAsync();
        if (_user != null)
            await _user.DisposeAsync();

        // Dispose any disposable handlers
        foreach (var handler in _actions.All)
        {
            if (handler is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else if (handler is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
