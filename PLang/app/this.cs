using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using app.Actor.Context;
using app.Settings;
using app.Errors;
using app.Variables;
using app.modules;
using Goal = app.Goals.Goal.@this;

namespace app;

/// <summary>
/// Main runtime for PLang App.
/// Executes goals and manages the execution lifecycle.
/// Self-contained: owns all app-level state (environment, culture, shutdown, key-value store).
/// </summary>
public sealed partial class @this : IAsyncDisposable
{
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly AppModules _modules;
    private readonly AppGoals _goals;
    private bool _disposed;

    private Actor.@this? _system;
    private Actor.@this? _user;
    private global::app.Services.@this? _services;

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
    /// App-scoped key/value store. Module-owned mutable state that must persist for
    /// the lifetime of the App goes here. Implements ISnapshot — round-trips with
    /// the rest of the App tree on Snapshot/Restore.
    /// TODO: replace with goal-backed dynamic property (see todos.md).
    /// </summary>
    public AppStatics Statics { get; } = new();

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
    public AppCode Code { get; } = new();

    /// <summary>
    /// Per-type navigator registry for Data navigation.
    /// </summary>
    public Variables.Navigators.@this Navigators { get; } = new();

    /// <summary>
    /// The loaded goals.
    /// </summary>
    public AppGoals Goals => _goals;

    /// <summary>
    /// The file system abstraction.
    /// </summary>
    public app.FileSystem.IPLangFileSystem FileSystem { get; set; }

    /// <summary>
    /// Pluggable step cache. Default: in-memory. Swap via: - use 'redis.dll' for caching
    /// </summary>
    public ICache Cache { get; set; } = new global::app.Cache.Memory();

    /// <summary>
    /// Strongly typed, goal-scoped module config.
    /// Navigation: app.Config.For&lt;archive.Config&gt;(context).Max
    /// </summary>
    public Config.@this Config { get; }

    /// <summary>
    /// App-level persistent key-value store backed by <c>.db/system.sqlite</c>
    /// (or in-memory under Tester.IsEnabled). One per app — actors share it.
    /// Modules own their tables (<c>encryption</c>, <c>settings</c>, <c>llm-cache</c>, etc.).
    /// Created lazily on first access so tests with fictional paths and apps
    /// that never touch settings don't pay for SQLite-file creation at boot.
    /// </summary>
    public global::app.Settings.IStore SettingsStore => _settingsStore.Value;
    private Lazy<global::app.Settings.IStore> _settingsStore = null!;

    /// <summary>
    /// Shared (one per app) settings collection. Holds Data values keyed by
    /// name, backed by <see cref="SettingsStore"/>. Registered on every actor's
    /// Variables via <see cref="Variables.@this.RegisterNavigable"/> so
    /// <c>%Settings.X%</c> resolution dispatches into <see cref="app.Settings.@this.Get"/>.
    /// </summary>
    public global::app.Settings.@this Settings { get; }

    /// <summary>
    /// Debug mode controller. Registers event handlers for step/goal debug output.
    /// </summary>
    public Debugging Debug { get; }

    /// <summary>
    /// Run-wide error scope. AsyncLocal-flowed current error (PLang <c>%!error%</c>) +
    /// audit list of every error pushed. Populated by error.handle.Wrap during recovery.
    /// </summary>
    public global::app.Errors.@this Errors { get; }

    /// <summary>
    /// Test runner. Discovers and runs *.test.goal files with assertion tracking.
    /// </summary>
    public global::app.Tester.@this Tester { get; }

    /// <summary>
    /// Builder mode controller. When enabled, actors use in-memory datasources.
    /// </summary>
    public global::app.Builder.@this Builder { get; }

    /// <summary>
    /// Callback subsystem config holder. NOT an ICallback — this is `app.Callback.*` config
    /// (e.g. `app.Callback.Signature.Expires`). Stage 4 expands the surface; Stage 3
    /// ships only the signature config.
    /// </summary>
    public global::app.Callback.@this Callback { get; } = new();

    /// <summary>
    /// Allow creating a new app if none exists. Set via --app={"create":true}. Default false.
    /// </summary>
    public bool Create { get; set; }

    /// <summary>
    /// Centralized type identity: PLang names ↔ CLR types. File-format
    /// characteristics live on <see cref="Formats"/>.
    /// </summary>
    public Types.@this Types { get; }

    /// <summary>
    /// File-format characteristics: extension → Kind, extension → MIME,
    /// Kind → compressibility. One per app.
    /// </summary>
    public Formats.@this Formats { get; } = new();

    /// <summary>
    /// System actor — the root of the cancellation hierarchy.
    /// Cancelling System cascades to User and Service.
    /// Links to App's shutdown token so RequestShutdown() cascades through everything.
    /// </summary>
    public Actor.@this System => _system ??= new Actor.@this("System", this, _shutdownCts.Token);

    /// <summary>
    /// User actor for end user operations. Links to System's cancellation token.
    /// </summary>
    public Actor.@this User => _user ??= new Actor.@this("User", this, System.CancellationToken);

    /// <summary>
    /// Flat per-call Service collection. Each Service is one outbound call's I/O
    /// scope (channels, identity, parent ref). Stage 7: replaces runtime1's
    /// Service-as-actor model.
    /// </summary>
    public global::app.Services.@this Services => _services ??= new global::app.Services.@this(this);

    /// <summary>
    /// The currently executing actor. Defaults to User. Changed to System during bootstrap (Start).
    /// app.execute switches temporarily for context-crossing dispatch.
    /// </summary>
    public Actor.@this CurrentActor { get; set; } = null!; // initialized to User in constructor

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
    /// App-level "keep alive" collection. Add disposable objects to extend their
    /// lifetime to the app; removed-and-disposed via Remove(x); all entries
    /// disposed on App.DisposeAsync.
    /// </summary>
    public KeepAlive.@this KeepAlive { get; } = new();

    /// <summary>
    /// App-wide call tree. Structural data (Action / Caller / Cause / Errors)
    /// is always captured; richer capture (timing, tags, history) is gated by
    /// <see cref="CallStack.@this.Flags"/>, populated via Debug.Apply from
    /// <c>--debug={callstack:{...}}</c>.
    /// </summary>
    public CallStack.@this CallStack { get; } = new();

    public @this(app.FileSystem.IPLangFileSystem fileSystem)
        : this(fileSystem.RootDirectory, fileSystem: fileSystem)
    {
    }

    public @this(string absolutePath, AppModules? modules = null,
        app.FileSystem.IPLangFileSystem? fileSystem = null,
        string? environment = null,
        bool autoWireConsoleChannels = true)
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
        Tester = new global::app.Tester.@this(this);
        Builder = new global::app.Builder.@this(this);
        Types = new Types.@this();
        Config = new Config.@this();
        _settingsStore = new Lazy<global::app.Settings.IStore>(CreateSettingsStore);
        Settings = new global::app.Settings.@this(this);
        _modules = modules ?? new AppModules();
        _modules.App = this;
        _goals = new AppGoals { App = this };
        FileSystem = fileSystem ?? CreateDefaultFileSystem(absolutePath);

        Errors = new global::app.Errors.@this(this);

        Code.RegisterDefaults();
        Types.RegisterDomainTypes();
        Navigators.RegisterDefaults();

        // Default actor is User — Start() switches to System for bootstrap
        CurrentActor = User;

        // Auto-wire console channels for ad-hoc App constructions (sub-process
        // test fixtures, embedded scenarios, C# tests). Entry points that own
        // the wiring (PlangConsole's Executor) construct with autoWireConsoleChannels:false
        // and call WireDefaultConsoleChannels themselves.
        if (autoWireConsoleChannels)
        {
            WireDefaultConsoleChannels(System);
            WireDefaultConsoleChannels(User);
        }
    }

    /// <summary>
    /// Verifies the all-three-roles invariant on every I/O actor (System, User).
    /// Returns Data.Error on first missing channel; Ok otherwise. PlangConsole
    /// (or any entry point) must register Output/Error/Input on each before
    /// goal execution. <see cref="Start"/> calls this and surfaces failure as
    /// MissingRequiredChannelAtBoot before any user code runs.
    /// </summary>
    /// <summary>
    /// Wires the console standard streams onto the given actor's Channels under
    /// the well-known names ("output", "error", "input"). PlangConsole calls
    /// this for System and User after constructing the App.
    /// </summary>
    public static void WireDefaultConsoleChannels(global::app.Actor.@this actor)
    {
        if (!actor.Channels.Contains(global::app.Channels.@this.Output))
            actor.Channels.Register(new global::app.Channels.Channel.Stream.@this(
                global::app.Channels.@this.Output, Console.OpenStandardOutput(),
                global::app.Channels.Channel.ChannelDirection.Output, ownsStream: false));
        if (!actor.Channels.Contains(global::app.Channels.@this.Error))
            actor.Channels.Register(new global::app.Channels.Channel.Stream.@this(
                global::app.Channels.@this.Error, Console.OpenStandardError(),
                global::app.Channels.Channel.ChannelDirection.Output, ownsStream: false));
        if (!actor.Channels.Contains(global::app.Channels.@this.Input))
            actor.Channels.Register(new global::app.Channels.Channel.Stream.@this(
                global::app.Channels.@this.Input, Console.OpenStandardInput(),
                global::app.Channels.Channel.ChannelDirection.Input, ownsStream: false));
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
    /// <summary>
    /// CamelCase + indented JsonSerializerOptions. Pure config bag — `static readonly` is the
    /// Rule C exception class for stateless option holders. Internal so tests can route
    /// through it; production callers (App.Save, data.Compare) use copies on their own type.
    /// </summary>
    internal static readonly JsonSerializerOptions CamelCaseIndented = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new Channels.Serializers.TimeSpanIso8601() }
    };

    public async Task<data.@this> Save()
    {
        Updated = DateTime.UtcNow;
        if (Created == default) Created = Updated;
        var json = JsonSerializer.Serialize(
            new { id = Id, name = Name, created = Created, updated = Updated, version = Version },
            CamelCaseIndented);
        var path = FileSystem.ValidatePath(".build/app.pr");
        var dir = global::System.IO.Path.GetDirectoryName(path);
        if (dir != null && !FileSystem.Directory.Exists(dir))
            FileSystem.Directory.CreateDirectory(dir);
        await FileSystem.File.WriteAllTextAsync(path, json);
        return app.data.@this.Ok(this);
    }

    /// <summary>
    /// Loads the PLang runtime via file.read on first use.
    /// file.read handles .pr → List&lt;Goal&gt; deserialization via MIME type mapping.
    // --- [Method] primitives — the kernel ---

    /// <summary>
    /// Runs a strongly-typed action. Properties are already set via init.
    /// Used by C# code composing actions (providers, tests).
    /// </summary>
    public async Task<data.@this<TResult>> RunAction<TAction, TResult>(TAction action, Actor.Context.@this context)
        where TAction : ICodeGenerated
    {
        var result = await action.ExecuteAsync(null!, context);
        if (!result.Success) return data.@this<TResult>.FromError(result.Error!);
        return data.@this<TResult>.Ok((TResult)result.Value!);
    }

    /// <summary>
    /// Runs a strongly-typed action and returns the raw Data result.
    /// Used by C# code composing actions (providers, tests).
    /// </summary>
    public async Task<data.@this> RunAction<TAction>(TAction action, Actor.Context.@this context)
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
    /// [Code] properties, resets backing fields, validates [IsNotNull], then calls
    /// Run() directly. App.Run wraps it. Return variable mapping is owned by
    /// Action.RunAsync, not here.
    /// </summary>
    public async Task<data.@this> Run(Goals.Goal.Steps.Step.Actions.Action.@this action, Actor.Context.@this context, CallStack.Call.@this? cause = null)
    {
        var (handler, error) = Modules.GetCodeGenerated(action);
        if (error != null) return data.@this.FromError(error);

        // CallStackOverflowException (depth limit or ContainsGoal cycle) trips at Push,
        // before the call frame is on the stack — catch it here so App.Run's contract
        // (returns Data, never throws) holds. Once Push succeeds the Call owns its
        // own try/catch via ExecuteAsync.
        CallStack.Call.@this call;
        try { call = CallStack.Push(action, context.Variables, cause); }
        catch (Errors.CallStackOverflowException ex) { return HandleOverflow(ex, action.Step, CallStack); }

        // Dispose order matters: anchor restore must run BEFORE Call's await-using
        // dispose (AsyncLocal restore, Children removal, Variables.OnSet unsubscribe).
        // C# disposes in reverse declaration order — declare `await using call` first
        // so the inner `using anchor` disposes first.
        await using var _ = call;
        using var _anchor = context.AnchorScope(action);
        return await call.ExecuteAsync(handler!, context);
    }

    private static data.@this HandleOverflow(Errors.CallStackOverflowException ex, Step? step, CallStack.@this stack)
    {
        var caller = stack.Current;
        var chain = caller != null ? caller.SnapshotChain() : Array.Empty<CallStack.Call.@this>();
        var overflowErr = new Errors.ServiceError(ex.Message, step!, chain, "CallStackOverflow", 500) { Exception = ex };
        stack.Audit.Add(overflowErr);
        return data.@this.FromError(overflowErr);
    }

    /// <summary>
    /// Bootstrap: loads app identity, resolves the goal file, runs it.
    /// Building is routed to the PLang builder (system/builder/).
    /// </summary>
    public async Task<data.@this> Start(Actor.Context.@this? context = null)
    {
        await Load();

        // Invariant: every I/O actor must have all three role-channels registered
        // by the entry point before goal execution. Surface a clear error otherwise.
        foreach (var actor in new[] { System, User })
        {
            var invariant = actor.Channels.Verify();
            if (!invariant.Success) return invariant;
        }

        // Foundational set: capture the boot-time channels so goal channels can
        // resolve their writes against the originals (recursion isolation).
        System.FreezeFoundational();
        User.FreezeFoundational();

        context ??= System.Context;
        CurrentActor = System;

        // Build → PLang builder (runs as User — user is building their code)
        if (Builder.IsEnabled) return await Builder.RunAsync();

        // Resolve goal file
        var goalFile = context.Variables.GetValue("goalFile") as string;
        if (string.IsNullOrEmpty(goalFile))
            return app.data.@this.FromError(new global::app.Errors.ServiceError(
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
    public async Task<data.@this> RunGoalAsync(GoalCall goalCall, Actor.Context.@this? context = null, CancellationToken ct = default)
    {
        context ??= User.Context;
        var goalResult = await goalCall.GetGoalAsync(this, context);
        if (!goalResult.Success) return goalResult;

        // Inject parameters — GetGoalAsync only injects when loading from file,
        // but goals found in memory (app.Goals.Get) need parameters too.
        // Goal-call is *not* a fork: it stays in the caller's flow. Variables.Set
        // is overlay-aware — if a fork operator above us (channel fire, parallel
        // foreach iteration) pushed a Calls scope, these writes land in that
        // scope; otherwise they go to the actor-shared dict. Either way,
        // sequential goal.call shares state with its caller (LoadUser leak still
        // works in plain top-of-flow code), and concurrent invocations are
        // isolated by whatever forked them.
        if (goalCall.Parameters != null)
            foreach (var param in goalCall.Parameters)
                context.Variables.Set(param.Name, param);

        return await ((Goal)goalResult.Value!).RunAsync(context);
    }

    /// <summary>
    /// Runs a goal already in memory. Delegates to Goal.RunAsync.
    /// </summary>
    public async Task<data.@this> RunGoalAsync(Goal goal, Actor.Context.@this? context = null, CancellationToken ct = default)
    {
        context ??= User.Context;
        return await goal.RunAsync(context);
    }

    private global::app.Settings.IStore CreateSettingsStore()
    {
        // Testing: in-memory db scoped by App.Id so per-test Apps never share state.
        // SQLite's shared-cache merges in-memory dbs with identical DataSource names,
        // so the App.Id scoping is load-bearing.
        if (Tester.IsEnabled)
            return global::app.Settings.Sqlite.InMemory($"system-{Id}");

        var dbDir = FileSystem.Path.Combine(AbsolutePath, ".db");
        var dbPath = FileSystem.Path.Combine(dbDir, "system.sqlite");
        return new global::app.Settings.Sqlite(dbPath, FileSystem);
    }

    private static app.FileSystem.IPLangFileSystem CreateDefaultFileSystem(string rootPath)
    {
        try
        {
            var fullPath = global::System.IO.Path.GetFullPath(rootPath);
            return new app.FileSystem.Default.PLangFileSystem(fullPath, "");
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            // If rootPath is not a valid filesystem path (e.g., "/app" in tests),
            // fall back to PLangFileSystem with current directory
            return new app.FileSystem.Default.PLangFileSystem(global::System.IO.Directory.GetCurrentDirectory(), "");
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
        if (_user != null)
            await _user.DisposeAsync();

        await _modules.DisposeAsync();
        await Code.DisposeAsync();
        await KeepAlive.DisposeAsync();
        if (_settingsStore.IsValueCreated) _settingsStore.Value.Dispose();
    }
}
