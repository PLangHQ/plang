using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using App.Actor.Context;
using App.Settings;
using App.Errors;
using App.Variables;
using App.modules;
using Goal = App.Goals.Goal.@this;

namespace App;

/// <summary>
/// Main runtime for PLang App.
/// Executes goals and manages the execution lifecycle.
/// Self-contained: owns all app-level state (environment, culture, shutdown, key-value store).
/// </summary>
public sealed class @this : Data.@this<@this>, IAsyncDisposable
{
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly AppModules _modules;
    private readonly AppGoals _goals;
    private readonly List<object> _keepAlive = new();
    private bool _disposed;

    private Actor.@this? _system;
    private Actor.@this? _service;
    private Actor.@this? _user;

    /// <summary>
    /// Unique identifier for this app. Loaded from app.pr, or generated on first run.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// Name of this app.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// When the app was first created.
    /// </summary>
    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    /// <summary>
    /// When the app was last updated.
    /// </summary>
    [JsonPropertyName("updated")]
    public DateTime Updated { get; set; }

    /// <summary>
    /// Version of the builder used.
    /// </summary>
    [JsonPropertyName("version")]
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
    /// The OS absolute path to the os/ folder (next to the plang executable).
    /// System goals (builder, events, etc.) are resolved from os/system/... here.
    /// Null when no os directory is configured.
    /// </summary>
    public string? OsDirectory { get; set; }

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
    /// When the app was started.
    /// </summary>
    public DateTime StartedAt { get; }

    /// <summary>
    /// How long the app has been running.
    /// </summary>
    public TimeSpan Uptime => DateTime.UtcNow - StartedAt;

    /// <summary>
    /// Cancellation token for graceful shutdown.
    /// </summary>
    public CancellationToken ShutdownToken => _shutdownCts.Token;

    /// <summary>
    /// App-scoped static storage for modules. Persists for the lifetime of the app.
    /// TODO: Replace with goal-backed dynamic property (see todos.md).
    /// </summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, object?>> _statics = new();
    internal ConcurrentDictionary<string, object?> GetStatic(string key) =>
        _statics.GetOrAdd(key, _ => new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Global event collection for the application.
    /// </summary>
    public AppEvents Events { get; }

    /// <summary>
    /// Flat action registry. Discovers, registers, and resolves actions by module.action.
    /// Built-in actions from PLang assembly, external DLLs add via Discover().
    /// </summary>
    public AppModules Modules => _modules;

    /// <summary>
    /// Type-keyed provider registry for pluggable module implementations.
    /// Modules define provider interfaces, register defaults, PLang developers override via DLL.
    /// </summary>
    public AppProviders Providers { get; } = new();

    /// <summary>
    /// Per-type navigator registry for Data navigation.
    /// </summary>
    public Data.Navigators.@this Navigators { get; } = new();

    /// <summary>
    /// The loaded goals.
    /// </summary>
    public AppGoals Goals => _goals;

    /// <summary>
    /// The file system abstraction.
    /// </summary>
    public App.FileSystem.IPLangFileSystem FileSystem { get; set; }

    /// <summary>
    /// I/O operations (file reading, channels).
    /// </summary>
    public AppChannels Channels { get; }

    /// <summary>
    /// Pluggable step cache. Default: in-memory. Swap via: - use 'redis.dll' for caching
    /// </summary>
    public ICache Cache { get; set; } = new MemoryStepCache();

    /// <summary>
    /// Strongly typed, goal-scoped module config.
    /// Navigation: app.Config.For&lt;archive.Config&gt;(context).Max
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
    /// Run-wide error scope. AsyncLocal-flowed current error (PLang <c>%!error%</c>) +
    /// audit list of every error pushed. Populated by error.handle.Wrap during recovery.
    /// </summary>
    public global::App.Errors.@this Errors { get; } = new();

    /// <summary>
    /// Test runner. Discovers and runs *.test.goal files with assertion tracking.
    /// </summary>
    public Testing Testing { get; }

    /// <summary>
    /// Builder mode controller. When enabled, actors use in-memory datasources.
    /// </summary>
    public global::App.Build.@this Build { get; }

    /// <summary>
    /// Allow creating a new app if none exists. Set via --app={"create":true}. Default false.
    /// </summary>
    public bool Create { get; set; }

    /// <summary>
    /// Centralized type knowledge: PLang names ↔ CLR types, file extensions → Kind/MIME, compressibility.
    /// </summary>
    public Types.@this Types { get; }

    /// <summary>
    /// System actor — the root of the cancellation hierarchy.
    /// Cancelling System cascades to User and Service.
    /// Links to App's shutdown token so RequestShutdown() cascades through everything.
    /// </summary>
    public Actor.@this System => _system ??= new Actor.@this("System", this, _shutdownCts.Token);

    /// <summary>
    /// Service actor for external service operations. Links to System's cancellation token.
    /// </summary>
    public Actor.@this Service => _service ??= new Actor.@this("Service", this, System.CancellationToken);

    /// <summary>
    /// User actor for end user operations. Links to System's cancellation token.
    /// </summary>
    public Actor.@this User => _user ??= new Actor.@this("User", this, System.CancellationToken);

    /// <summary>
    /// The currently executing actor. Defaults to User. Changed to System during bootstrap (Start).
    /// app.execute switches temporarily for context-crossing dispatch.
    /// </summary>
    public Actor.@this CurrentActor { get; set; } = null!; // initialized to User in constructor

    /// <summary>
    /// Context of the current executor.
    /// </summary>
    public Actor.Context.@this Context => CurrentActor.Context;
    public Variables.@this Variables => Context.Variables;

    /// <summary>
    /// Resolves an actor by name. Returns error instead of null — object reports its own errors.
    /// </summary>
    public (Actor.@this? Actor, IError? Error) GetActor(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return (null, new ActionError("Actor name is required", "ActorRequired", 400));

        var actor = name.ToLowerInvariant() switch
        {
            "system" => System,
            "service" => Service,
            "user" => User,
            _ => (Actor.@this?)null
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
    /// Promotes an object to app-level lifetime. Disposed on App.DisposeAsync.
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

    public @this(App.FileSystem.IPLangFileSystem fileSystem)
        : this(fileSystem.RootDirectory, fileSystem: fileSystem)
    {
    }

    public @this(string absolutePath, AppModules? modules = null,
        App.FileSystem.IPLangFileSystem? fileSystem = null,
        string? environment = null)
        : base("!app")
    {
        Id = Guid.NewGuid().ToString("N")[..12];
        var trimmed = absolutePath.TrimEnd('/', '\\');
        var lastSep = trimmed.LastIndexOfAny(['/', '\\']);
        Name = lastSep >= 0 ? trimmed[(lastSep + 1)..] : trimmed;
        AbsolutePath = absolutePath;
        Environment = environment ?? "production";
        StartedAt = DateTime.UtcNow;
        Events = new AppEvents();
        Debug = new Debugging(this);
        Testing = new Testing(this);
        Build = new global::App.Build.@this(this);
        Types = new Types.@this();
        Config = new Config.@this();
        SettingsVariable = new SettingsVariable(this);
        _modules = modules ?? new AppModules();
        _goals = new AppGoals { App = this };
        FileSystem = fileSystem ?? CreateDefaultFileSystem(absolutePath);
        Channels = new AppChannels(this);

        Providers.RegisterDefaults();
        Types.RegisterDomainTypes();
        Navigators.RegisterDefaults();

        // Default actor is User — Start() switches to System for bootstrap
        CurrentActor = User;
    }

    /// <summary>
    /// Loads app identity from .build/app.pr. Called at startup.
    /// If no app.pr exists, the app keeps its generated Id.
    /// </summary>
    public async Task Load()
    {
        var path = FileSystem.ValidatePath(".build/app.pr");
        if (!FileSystem.File.Exists(path)) return;
        var json = await FileSystem.File.ReadAllTextAsync(path);
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("id", out var idProp)) Id = idProp.GetString() ?? Id;
            if (root.TryGetProperty("name", out var nameProp)) Name = nameProp.GetString() ?? Name;
            if (root.TryGetProperty("created", out var createdProp) && createdProp.TryGetDateTime(out var created)) Created = created;
            if (root.TryGetProperty("updated", out var updatedProp) && updatedProp.TryGetDateTime(out var updated)) Updated = updated;
            if (root.TryGetProperty("version", out var versionProp)) Version = versionProp.GetString();
        }
        catch (JsonException) { /* corrupt app.pr — keep generated identity */ }
    }

    /// <summary>
    /// Saves app identity to .build/app.pr.
    /// </summary>
    public async Task<Data.@this> Save()
    {
        Updated = DateTime.UtcNow;
        if (Created == default) Created = Updated;
        var json = JsonSerializer.Serialize(
            new { id = Id, name = Name, created = Created, updated = Updated, version = Version },
            Utils.Json.CamelCaseIndented);
        var path = FileSystem.ValidatePath(".build/app.pr");
        var dir = global::System.IO.Path.GetDirectoryName(path);
        if (dir != null && !FileSystem.Directory.Exists(dir))
            FileSystem.Directory.CreateDirectory(dir);
        await FileSystem.File.WriteAllTextAsync(path, json);
        return App.Data.@this.Ok(this);
    }

    /// <summary>
    /// Loads the PLang runtime via file.read on first use.
    /// file.read handles .pr → List&lt;Goal&gt; deserialization via MIME type mapping.
    // --- [Method] primitives — the kernel ---

    /// <summary>
    /// Runs a strongly-typed action. Properties are already set via init.
    /// Used by C# code composing actions (providers, tests).
    /// </summary>
    public async Task<Data.@this<TResult>> RunAction<TAction, TResult>(TAction action, Actor.Context.@this context)
        where TAction : ICodeGenerated
    {
        var result = await action.ExecuteAsync(null!, context);
        if (!result.Success) return Data.@this<TResult>.FromError(result.Error!);
        return Data.@this<TResult>.Ok((TResult)result.Value!);
    }

    /// <summary>
    /// Runs a strongly-typed action and returns the raw Data result.
    /// Used by C# code composing actions (providers, tests).
    /// </summary>
    public async Task<Data.@this> RunAction<TAction>(TAction action, Actor.Context.@this context)
        where TAction : ICodeGenerated
    {
        return await action.ExecuteAsync(null!, context);
    }

    /// <summary>
    /// Dispatches an action through the production execution path: pushes a Call onto
    /// the CallStack tree (AsyncLocal Current updates), saves/restores Context anchors
    /// (Step/Goal/Event), populates Call.Errors + stack.Audit on failure, and translates
    /// CLR exceptions into ServiceError with the post-Push chain attached.
    ///
    /// The generated handler ExecuteAsync is thin — it sets markers, eagerly resolves
    /// [Provider] properties, resets backing fields, validates [IsNotNull], then calls
    /// Run() directly. App.Run wraps it. Return variable mapping is owned by
    /// Action.RunAsync, not here.
    /// </summary>
    public async Task<Data.@this> Run(Goals.Goal.Steps.Step.Actions.Action.@this action, Actor.Context.@this context, CallStack.Call.@this? cause = null)
    {
        var (handler, error) = Modules.GetCodeGenerated(action);
        if (error != null)
            return Data.@this.FromError(error);

        var step = action.Step;
        var stack = Debug.CallStack;

        // Push BEFORE the handler runs so call.SnapshotChain() inside the catch reflects
        // "self at index [0]" — the failing Call IS in the chain (behavior tweak vs old
        // shape that captured pre-push frames).
        await using var call = stack.Push(action, context.Variables, cause);

        // Save context anchors so nested dispatch can mutate them and we restore on the way out.
        var previousStep = context.Step;
        var previousGoal = context.Goal;
        var previousEvent = context.Event;
        context.Step = action.Step;
        if (context.Step != null) context.Step.Context = context;
        context.Goal = action.Step?.Goal;

        try
        {
            var result = await handler!.ExecuteAsync(action, context);
            // Stamp __SnapshotParams onto Error.Params if the handler returned an error
            // without one already populated. (Generator no longer attaches snapshots
            // inside ExecuteAsync — that responsibility moved here in v4 Phase 3.)
            if (!result.Success && result.Error is global::App.Errors.Error err)
            {
                if (err.Params == null) err.Params = handler.SnapshotParams();
                // Capture the failing Call chain so error.handle (and other downstream
                // observers) can identify the failing Call after this scope's Push pops.
                // Snapshot is index-[0]=self, walking Caller upward — matches the
                // ServiceError catch path below.
                if (err.CallFrames.Count == 0) err.CallFrames = call.SnapshotChain();
                call.Errors.Add(result.Error!);
                stack.Audit.Add(result.Error!);
            }
            return result;
        }
        // Deliberately catches OperationCanceledException — timeout.after depends on this:
        // the inner action's generated ExecuteAsync swallows OCE into a ServiceError result,
        // so timeout.after detects the timeout via CTS state + failed result, not via OCE
        // bubbling up. Step.RunAsync's catch DOES exclude OCE — that asymmetry is intentional.
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            var serviceErr = new global::App.Errors.ServiceError(
                ex.Message, step!, call.SnapshotChain(), "ServiceError", 400) { Exception = ex };
            serviceErr.Params = handler!.SnapshotParams();
            call.Errors.Add(serviceErr);
            stack.Audit.Add(serviceErr);
            return Data.@this.FromError(serviceErr);
        }
        finally
        {
            // Anchor restore runs before the `await using` dispose — the Call's own
            // bookkeeping (AsyncLocal restore, Children removal when history off,
            // Variables.OnSet unsubscribe) doesn't depend on Step/Goal/Event being set.
            context.Step = previousStep;
            context.Goal = previousGoal;
            context.Event = previousEvent;
        }
    }

    /// <summary>
    /// Bootstrap: loads app identity, resolves the goal file, runs it.
    /// Building is routed to the PLang builder (system/builder/).
    /// </summary>
    public async Task<Data.@this> Start(Actor.Context.@this? context = null)
    {
        await Load();

        context ??= System.Context;
        CurrentActor = System;

        // Build → PLang builder (runs as User — user is building their code)
        if (Build.IsEnabled)
        {
            // Safety check: confirm new app creation if no app.pr exists.
            // --app={"create":true} skips the prompt. Headless/CI defaults to "no".
            var appPrPath = FileSystem.ValidatePath(".build/app.pr");
            if (!FileSystem.File.Exists(appPrPath) && !Create)
            {
                if (Console.IsInputRedirected)
                    return Data.@this.FromError(new global::App.Errors.ServiceError(
                        $"No app found at {AbsolutePath}. Run plang build from your app's root directory, or use --app={{\"create\":true}}.", "NoAppFound", 400));

                Console.Write($"No app found at {AbsolutePath}. Create new app? (y/n): ");
                var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (answer != "y" && answer != "yes")
                    return Data.@this.FromError(new global::App.Errors.ServiceError(
                        "Build cancelled. Run plang build from your app's root directory.", "BuildCancelled", 400));
            }

            CurrentActor = User;
            var buildCall = new GoalCall { Name = "Build", PrPath = "/system/builder/.build/build.pr" };
            return await RunGoalAsync(buildCall, User.Context);
        }

        // Resolve goal file
        var goalFile = context.Variables.GetValue("goalFile") as string;
        if (string.IsNullOrEmpty(goalFile))
            return App.Data.@this.FromError(new global::App.Errors.ServiceError(
                "No goal file specified. Use: plang <goalfile>", "NoGoalFile", 400));

        var goalCall = new GoalCall { PrPath = goalFile };
        var goalResult = await goalCall.GetGoalAsync(this, context);
        if (!goalResult.Success) return goalResult;

        var goal = (Goal)goalResult.Value!;

        // Switch to user actor for user code execution
        CurrentActor = User;
        return await goal.RunAsync(User.Context);
    }

    /// <summary>
    /// Runs a goal via GoalCall. Resolves the goal then delegates to Goal.RunAsync.
    /// </summary>
    public async Task<Data.@this> RunGoalAsync(GoalCall goalCall, Actor.Context.@this? context = null, CancellationToken ct = default)
    {
        context ??= User.Context;
        var goalResult = await goalCall.GetGoalAsync(this, context);
        if (!goalResult.Success) return goalResult;

        // Inject parameters — GetGoalAsync only injects when loading from file,
        // but goals found in memory (app.Goals.Get) need parameters too
        if (goalCall.Parameters != null)
            foreach (var param in goalCall.Parameters)
                context.Variables.Set(param.Name, param);

        return await ((Goal)goalResult.Value!).RunAsync(context);
    }

    /// <summary>
    /// Runs a goal already in memory. Delegates to Goal.RunAsync.
    /// </summary>
    public async Task<Data.@this> RunGoalAsync(Goal goal, Actor.Context.@this? context = null, CancellationToken ct = default)
    {
        context ??= User.Context;
        return await goal.RunAsync(context);
    }

    private static App.FileSystem.IPLangFileSystem CreateDefaultFileSystem(string rootPath)
    {
        try
        {
            var fullPath = global::System.IO.Path.GetFullPath(rootPath);
            return new App.FileSystem.Default.PLangFileSystem(fullPath, "");
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            // If rootPath is not a valid filesystem path (e.g., "/app" in tests),
            // fall back to PLangFileSystem with current directory
            return new App.FileSystem.Default.PLangFileSystem(global::System.IO.Directory.GetCurrentDirectory(), "");
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

        // Dispose app-level channels
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
