using App.Context;
using App.Settings;
using App.Errors;
using App.Variables;
using App.modules;
using Goal = App.Goals.Goal.@this;
using System.Globalization;

namespace App;

/// <summary>
/// Main runtime engine for PLang App.
/// Executes goals and manages the execution lifecycle.
/// Self-contained: owns all app-level state (environment, culture, shutdown, key-value store).
/// </summary>
public sealed class @this : Data<@this>, IAsyncDisposable
{
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly EngineModules _modules;
    private readonly EngineGoals _goals;
    private readonly List<object> _keepAlive = new();
    private bool _disposed;

    private Actor? _system;
    private Actor? _service;
    private Actor? _user;

    /// <summary>
    /// Unique identifier for this app. Loaded from app.pr, or generated on first run.
    /// </summary>
    [global::System.Text.Json.Serialization.JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// Name of this app.
    /// </summary>
    [global::System.Text.Json.Serialization.JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// When the app was first created.
    /// </summary>
    [global::System.Text.Json.Serialization.JsonPropertyName("created")]
    public DateTime Created { get; set; }

    /// <summary>
    /// When the app was last updated.
    /// </summary>
    [global::System.Text.Json.Serialization.JsonPropertyName("updated")]
    public DateTime Updated { get; set; }

    /// <summary>
    /// Version of the builder used.
    /// </summary>
    [global::System.Text.Json.Serialization.JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>
    /// Relative root path, always "/".
    /// </summary>
    public new string Path => "/";

    /// <summary>
    /// The OS absolute path of the application (e.g. C:\myapp or /home/user/app).
    /// </summary>
    public string AbsolutePath { get; }

    /// <summary>
    /// The OS absolute path to the system/ folder (next to the plang executable).
    /// System goals (builder, events, etc.) are resolved from here.
    /// Null when no system directory is configured.
    /// </summary>
    public string? SystemDirectory { get; set; }

    /// <summary>
    /// Environment name (e.g., "production", "development").
    /// </summary>
    public string Environment { get; set; }

    /// <summary>
    /// Application culture for formatting dates, numbers, etc.
    /// Defaults to InvariantCulture.
    /// </summary>
    public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;

    /// <summary>
    /// When the engine was started.
    /// </summary>
    public DateTime StartedAt { get; }

    /// <summary>
    /// How long the engine has been running.
    /// </summary>
    public TimeSpan Uptime => DateTime.UtcNow - StartedAt;

    /// <summary>
    /// Cancellation token for graceful shutdown.
    /// </summary>
    public CancellationToken ShutdownToken => _shutdownCts.Token;

    /// <summary>
    /// Global event collection for the application.
    /// </summary>
    public EngineEvents Events { get; }

    /// <summary>
    /// Flat action registry. Discovers, registers, and resolves actions by module.action.
    /// Built-in actions from PLang assembly, external DLLs add via Discover().
    /// </summary>
    public EngineModules Modules => _modules;

    /// <summary>
    /// Type-keyed provider registry for pluggable module implementations.
    /// Modules define provider interfaces, register defaults, PLang developers override via DLL.
    /// </summary>
    public EngineProviders Providers { get; } = new();

    /// <summary>
    /// Per-type navigator registry for Data navigation.
    /// </summary>
    public Navigators.@this Navigators { get; } = new();

    /// <summary>
    /// The loaded goals.
    /// </summary>
    public EngineGoals Goals => _goals;

    /// <summary>
    /// The file system abstraction.
    /// </summary>
    public App.SafeFileSystem.IPLangFileSystem FileSystem { get; set; }

    /// <summary>
    /// I/O operations (file reading, channels).
    /// </summary>
    public EngineChannels Channels { get; }

    /// <summary>
    /// Pluggable step cache. Default: in-memory. Swap via: - use 'redis.dll' for caching
    /// </summary>
    public ICache Cache { get; set; } = new MemoryStepCache();

    /// <summary>
    /// Strongly typed, goal-scoped module config.
    /// Navigation: engine.Config.For&lt;archive.Config&gt;(context).Max
    /// </summary>
    public Config.@this Config { get; }

    /// <summary>
    /// Shared SettingsVariable instance registered on every actor's Variables.
    /// Provides %Settings.X% variable resolution from system DataSource.
    /// Single instance — all actors share the same object.
    /// </summary>
    internal SettingsVariable SettingsVariable { get; }

    /// <summary>
    /// Debug mode controller. Registers event handlers for step/goal debug output.
    /// </summary>
    public Debugging Debug { get; }

    /// <summary>
    /// Test runner. Discovers and runs *.test.goal files with assertion tracking.
    /// </summary>
    public Testing Testing { get; }

    /// <summary>
    /// Builder mode controller. When enabled, actors use in-memory datasources.
    /// </summary>
    public Build.@this Building { get; }

    /// <summary>
    /// Centralized type knowledge: PLang names ↔ CLR types, file extensions → Kind/MIME, compressibility.
    /// </summary>
    public Types.@this Types { get; }

    /// <summary>
    /// System actor for internal engine operations. Created lazily on first access.
    /// </summary>
    public Actor System => _system ??= new Actor("System", this);

    /// <summary>
    /// Service actor for external service operations. Created lazily on first access.
    /// </summary>
    public Actor Service => _service ??= new Actor("Service", this);

    /// <summary>
    /// User actor for end user operations. Created lazily on first access.
    /// </summary>
    public Actor User => _user ??= new Actor("User", this);

    /// <summary>
    /// The currently executing actor. Defaults to User. Changed to System during bootstrap (Start).
    /// engine.execute switches temporarily for context-crossing dispatch.
    /// </summary>
    public Actor CurrentActor { get; set; } = null!; // initialized to User in constructor

    /// <summary>
    /// Context of the current executor.
    /// </summary>
    public Context.@this Context => CurrentActor.Context;
    public Variables.@this Variables => Context.Variables;

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

    /// <summary>
    /// Requests graceful shutdown.
    /// </summary>
    public void RequestShutdown()
    {
        _shutdownCts.Cancel();
    }

    /// <summary>
    /// Promotes an object to engine-level lifetime. Disposed on Engine.DisposeAsync.
    /// Use for objects that must outlive their creating goal (e.g., background listeners).
    /// </summary>
    public void KeepAlive(object instance) => _keepAlive.Add(instance);

    /// <summary>
    /// Removes and disposes a KeepAlive object.
    /// </summary>
    public void RemoveKeepAlive(object instance)
    {
        if (!_keepAlive.Remove(instance)) return;
        if (instance is IAsyncDisposable ad) ad.DisposeAsync().AsTask().GetAwaiter().GetResult();
        else if (instance is IDisposable d) d.Dispose();
    }

    public @this(App.SafeFileSystem.IPLangFileSystem fileSystem)
        : this(fileSystem.RootDirectory, fileSystem: fileSystem)
    {
    }

    public @this(string absolutePath, EngineModules? modules = null,
        App.SafeFileSystem.IPLangFileSystem? fileSystem = null,
        string? environment = null)
        : base("!engine")
    {
        Id = Guid.NewGuid().ToString("N")[..12];
        AbsolutePath = absolutePath;
        Environment = environment ?? "production";
        StartedAt = DateTime.UtcNow;
        Events = new EngineEvents();
        Debug = new Debugging(this);
        Testing = new Testing(this);
        Building = new Build.@this(this);
        Types = new Types.@this();
        Config = new Config.@this();
        SettingsVariable = new SettingsVariable(this);
        _modules = modules ?? new EngineModules();
        _goals = new EngineGoals { App = this };
        FileSystem = fileSystem ?? CreateDefaultFileSystem(absolutePath);
        Channels = new EngineChannels(this);

        Providers.RegisterDefaults();
        Types.RegisterDomainTypes();
        Navigators.RegisterDefaults();

        // Default actor is User — Start() switches to System for bootstrap
        CurrentActor = User;
    }

    /// <summary>
    /// Loads app identity from .build/app.pr. Called at startup.
    /// If no app.pr exists, the engine keeps its generated Id.
    /// </summary>
    public async Task Load()
    {
        var path = FileSystem.ValidatePath(".build/app.pr");
        if (!FileSystem.File.Exists(path)) return;
        var json = await FileSystem.File.ReadAllTextAsync(path);
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            using var doc = global::System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("id", out var idProp)) Id = idProp.GetString() ?? Id;
            if (root.TryGetProperty("name", out var nameProp)) Name = nameProp.GetString() ?? Name;
            if (root.TryGetProperty("created", out var createdProp) && createdProp.TryGetDateTime(out var created)) Created = created;
            if (root.TryGetProperty("updated", out var updatedProp) && updatedProp.TryGetDateTime(out var updated)) Updated = updated;
            if (root.TryGetProperty("version", out var versionProp)) Version = versionProp.GetString();
        }
        catch (global::System.Text.Json.JsonException) { /* corrupt app.pr — keep generated identity */ }
    }

    /// <summary>
    /// Saves app identity to .build/app.pr.
    /// </summary>
    public async Task<Data> Save()
    {
        Updated = DateTime.UtcNow;
        if (Created == default) Created = Updated;
        var json = global::System.Text.Json.JsonSerializer.Serialize(
            new { id = Id, name = Name, created = Created, updated = Updated, version = Version },
            Utils.Json.CamelCaseIndented);
        var path = FileSystem.ValidatePath(".build/app.pr");
        var dir = global::System.IO.Path.GetDirectoryName(path);
        if (dir != null && !FileSystem.Directory.Exists(dir))
            FileSystem.Directory.CreateDirectory(dir);
        await FileSystem.File.WriteAllTextAsync(path, json);
        return Data.Ok(this);
    }

    /// <summary>
    /// Loads the PLang runtime via file.read on first use.
    /// file.read handles .pr → List&lt;Goal&gt; deserialization via MIME type mapping.
    // --- [Method] primitives — the kernel ---

    /// <summary>
    /// Runs a module action through the same code-generated execution path as steps.
    /// Set properties via init, then call RunAction — engine wires context, memory, validation, error handling.
    /// Usage: var result = await engine.RunAction&lt;Hash, string&gt;(new Hash { Data = x, Algorithm = "keccak256" }, context);
    /// </summary>
    public async Task<Data<TResult>> RunAction<TAction, TResult>(TAction action, Context.@this context)
        where TAction : ICodeGenerated
    {
        var emptyAction = new Goals.Goal.Steps.Step.Actions.Action.@this();
        var result = await action.ExecuteAsync(emptyAction, this, context);
        if (!result.Success) return Data<TResult>.FromError(result.Error!);
        return Data<TResult>.Ok((TResult)result.Value!);
    }

    /// <summary>
    /// Runs a module action and returns the raw Data result (preserves Signature, Properties, etc.).
    /// Use when the result convention puts data on properties other than Value (e.g., sign puts SignedData on Signature).
    /// </summary>
    public async Task<Data> RunAction<TAction>(TAction action, Context.@this context)
        where TAction : ICodeGenerated
    {
        var emptyAction = new Goals.Goal.Steps.Step.Actions.Action.@this();
        return await action.ExecuteAsync(emptyAction, this, context);
    }

    /// <summary>
    /// Dispatches a single action. The engine doesn't know about goals or steps.
    /// It receives an action, finds the module handler, executes it, returns result.
    /// </summary>
    public async Task<Data> Run(Goals.Goal.Steps.Step.Actions.Action.@this action, Context.@this context)
    {
        var (executor, error) = Modules.GetCodeGenerated(action.Module, action.ActionName, context);
        if (error != null)
            return Data.FromError(error);

        var result = await executor!.ExecuteAsync(action, this, context);

        // Handle return mapping — set variable on Variables
        if (action.Return != null)
        {
            foreach (var returnVar in action.Return)
            {
                result.Name = returnVar.Name;
                context.Variables.Put(result);
            }
        }

        return result;
    }

    /// <summary>
    /// Bootstrap: reads system/.build/run.pr, pushes its actions to Run().
    /// This is the ONLY loop in C#. After this, PLang code drives everything.
    /// </summary>
    public async Task<Data> Start(Context.@this? context = null)
    {
        await Load();

        context ??= System.Context;
        CurrentActor = System;

        var goalCall = new GoalCall { PrPath = "system/.build/run.pr" };
        var goal = await goalCall.GetGoalAsync(this, context);
        if (goal == null)
            return Data.FromError(new Errors.ServiceError(
                "system/.build/run.pr not found", "RuntimeNotFound", 500));

        return await RunSteps(goal.Steps, context);
    }

    /// <summary>
    /// Iterates steps and dispatches each action via Run().
    /// Used by Start() for bootstrap and by engine.execute for user steps.
    /// </summary>
    public async Task<Data> RunSteps(GoalSteps steps, Context.@this context)
    {
        Data result = Data.Ok();
        int? skipBelowIndent = null;

        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];

            // Sub-step skip: if condition was false, skip indented children
            if (skipBelowIndent != null)
            {
                if (step.Indent > skipBelowIndent)
                    continue;
                skipBelowIndent = null;
            }

            foreach (var action in step.Actions)
            {
                result = await Run(action, context);
                if (!result.Success && !result.Handled) return result;
                if (result.Returned) return result;
            }

            // Sub-step control: false condition skips indented children
            // Only condition module actions control sub-step flow — other modules
            // may return bool values (e.g., variable.set with false) without intent to skip.
            if (i + 1 < steps.Count && steps[i + 1].Indent > step.Indent
                && result.Value is bool conditionResult && !conditionResult
                && step.Actions.Count > 0
                && string.Equals(step.Actions[0].Module, "condition", StringComparison.OrdinalIgnoreCase))
            {
                skipBelowIndent = step.Indent;
            }
        }
        return result;
    }

    /// <summary>
    /// Runs a goal via GoalCall. Used by goal.call and backward compat.
    /// </summary>
    public async Task<Data> RunGoalAsync(GoalCall goalCall, Context.@this? context = null, CancellationToken ct = default)
    {
        context ??= User.Context;
        var goal = await goalCall.GetGoalAsync(this, context);
        if (goal == null)
            return Data.FromError(Errors.GoalError.NotFound(goalCall.Name ?? goalCall.PrPath ?? "unknown"));

        return await RunGoalAsync(goal, context, ct);
    }

    /// <summary>
    /// Kernel-executes a goal already in memory.
    /// </summary>
    public async Task<Data> RunGoalAsync(Goal goal, Context.@this? context = null, CancellationToken ct = default)
    {
        context ??= User.Context;

        if (ct.IsCancellationRequested)
            return Data.FromError(new Errors.Error("Operation was cancelled", "Cancelled", 499));

        var previousGoal = context.Goal;
        context.Goal = goal;
        try
        {
            return await RunSteps(goal.Steps, context);
        }
        finally
        {
            context.Goal = previousGoal;
        }
    }

    /// <summary>
    /// Creates a new execution context.
    /// </summary>
    public Context.@this CreateContext(Variables.@this? variables = null)
    {
        var context = new Context.@this(this, variables)
        {
            CallStack = new CallStack.@this()
        };
        return context;
    }

    private static App.SafeFileSystem.IPLangFileSystem CreateDefaultFileSystem(string rootPath)
    {
        try
        {
            var fullPath = global::System.IO.Path.GetFullPath(rootPath);
            return new App.SafeFileSystem.PLangFileSystem(fullPath, "");
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            // If rootPath is not a valid filesystem path (e.g., "/app" in tests),
            // fall back to PLangFileSystem with current directory
            return new App.SafeFileSystem.PLangFileSystem(global::System.IO.Directory.GetCurrentDirectory(), "");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Cancel shutdown token
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();

        // Dispose created actors
        if (_system != null)
            await _system.DisposeAsync();
        if (_service != null)
            await _service.DisposeAsync();
        if (_user != null)
            await _user.DisposeAsync();

        // Dispose any disposable handlers
        foreach (var handler in _modules.All)
        {
            if (handler is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else if (handler is IDisposable disposable)
                disposable.Dispose();
        }

        // Dispose providers (HttpClient, etc.)
        foreach (var provider in Providers.All())
        {
            if (provider is IAsyncDisposable asyncProv)
                await asyncProv.DisposeAsync();
            else if (provider is IDisposable disposableProv)
                disposableProv.Dispose();
        }

        // Dispose engine-level channels
        await Channels.DisposeAsync();

        // Dispose KeepAlive objects
        foreach (var d in _keepAlive)
        {
            if (d is IAsyncDisposable asyncKeep) await asyncKeep.DisposeAsync();
            else if (d is IDisposable disposableKeep) disposableKeep.Dispose();
        }
        _keepAlive.Clear();

    }
}
