using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;
using app.actor.context;
using app.module.settings;
using app.error;
using app.variable;
using app.module;
using app.Utils;
using Goal = app.goal.@this;

namespace app;

/// <summary>
/// Main runtime for PLang App.
/// Executes goals and manages the execution lifecycle.
/// Self-contained: owns all app-level state (environment, culture, shutdown, key-value store).
/// </summary>
public sealed partial class @this : IAsyncDisposable
{
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly global::app.module.@this _modules;
    private readonly global::app.goal.list.@this _goals;
    private bool _disposed;

    private actor.@this? _system;
    private actor.@this? _user;
    private global::app.service.list.@this? _services;

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
    /// Parent app, when this app was constructed as a child of another.
    /// A child inherits its parent's filesystem scope: <c>path.@this.IsInRoot</c>
    /// walks the Parent chain, so a child app rooted at a narrower
    /// subdirectory still treats the parent's <c>AbsolutePath</c> as in-root.
    /// Null for top-level apps.
    /// </summary>
    public app.@this? Parent { get; set; }

    /// <summary>
    /// The computed <c>os/</c> folder next to the executable. App-level constant
    /// (not file-scheme-specific): the path base's <c>Authorize</c> and
    /// <c>FilePath.ValidatePath</c> both anchor system goals against it, so it
    /// belongs on <c>app</c> rather than on a concrete path subclass.
    /// </summary>
    // The os/ root anchor. Pure name math against AppContext.BaseDirectory;
    // path.Resolve can't be used because App is still constructing and has
    // no Context yet, so this routes through PathHelper directly.
    public string OsAbsolutePath =>
        PathHelper.GetFullPath(PathHelper.Combine(AppContext.BaseDirectory, "os"));

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
    public global::app.@event.list.@this Event { get; }

    /// <summary>
    /// Flat action registry. Discovers, registers, and resolves actions by module.action.
    /// Built-in actions from PLang assembly, external DLLs add via Discover().
    /// </summary>
    public global::app.module.@this Module => _modules;

    /// <summary>
    /// Type-keyed provider registry for pluggable module implementations.
    /// Modules define provider interfaces, register defaults, PLang developers override via DLL.
    /// </summary>
    public AppCode Code { get; } = new();

    /// <summary>
    /// Per-type navigator registry for Data navigation.
    /// </summary>
    public variable.navigator.list.@this Navigator { get; } = new();

    /// <summary>
    /// The loaded goals.
    /// </summary>
    public global::app.goal.list.@this Goal => _goals;

    /// <summary>
    /// The file system abstraction.
    /// </summary>

    /// <summary>
    /// Pluggable step cache. Default: in-memory. Swap via: - use 'redis.dll' for caching
    /// </summary>
    public ICache Cache { get; set; } = new global::app.module.cache.Memory();

    /// <summary>
    /// Strongly typed, goal-scoped module config.
    /// Navigation: app.config.For&lt;archive.Config&gt;(context).Max
    /// </summary>
    public config.@this Config { get; }

    /// <summary>
    /// App-level persistent key-value store backed by <c>.db/system.sqlite</c>
    /// (or in-memory under Tester.IsEnabled). One per app — actors share it.
    /// Modules own their tables (<c>encryption</c>, <c>settings</c>, <c>llm-cache</c>, etc.).
    /// Created lazily on first access so tests with fictional paths and apps
    /// that never touch settings don't pay for SQLite-file creation at boot.
    /// </summary>
    public global::app.module.settings.IStore SettingsStore => _settingsStore.Value;
    private Lazy<global::app.module.settings.IStore> _settingsStore = null!;

    /// <summary>
    /// Shared (one per app) settings collection. Holds Data values keyed by
    /// name, backed by <see cref="SettingsStore"/>. Registered on every actor's
    /// Variables via <see cref="Variables.@this.RegisterNavigable"/> so
    /// <c>%Settings.X%</c> resolution dispatches into <see cref="app.module.settings.@this.Get"/>.
    /// </summary>
    public global::app.module.settings.@this Settings { get; }

    /// <summary>
    /// Debug mode controller. Registers event handlers for step/goal debug output.
    /// </summary>
    public Debugging Debug { get; }

    /// <summary>
    /// Run-wide error scope. AsyncLocal-flowed current error (PLang <c>%!error%</c>) +
    /// audit list of every error pushed. Populated by error.handle.Wrap during recovery.
    /// </summary>
    public global::app.error.list.@this Error { get; }

    /// <summary>
    /// Test runner. Discovers and runs *.test.goal files with assertion tracking.
    /// </summary>
    public global::app.tester.@this Tester { get; }

    /// <summary>
    /// Builder mode controller. When enabled, actors use in-memory datasources.
    /// </summary>
    public global::app.module.builder.@this Builder { get; }

    /// <summary>
    /// Allow creating a new app if none exists. Set via --app={"create":true}. Default false.
    /// </summary>
    public bool Create { get; set; }

    /// <summary>
    /// Centralized type identity: PLang names ↔ CLR types. File-format
    /// characteristics live on <see cref="Format"/>.
    /// </summary>
    public type.catalog.@this Type { get; }

    /// <summary>
    /// File-format characteristics: extension → Kind, extension → MIME,
    /// Kind → compressibility. One per app.
    /// </summary>
    public format.list.@this Format { get; } = new();

    /// <summary>
    /// System actor — the root of the cancellation hierarchy.
    /// Cancelling System cascades to User and Service.
    /// Links to App's shutdown token so RequestShutdown() cascades through everything.
    /// </summary>
    public actor.@this System => _system ??= new actor.@this("System", this, _shutdownCts.Token);

    /// <summary>
    /// User actor for end user operations. Links to System's cancellation token.
    /// </summary>
    public actor.@this User => _user ??= new actor.@this("User", this, System.CancellationToken);

    /// <summary>
    /// Flat per-call Service collection. Each Service is one outbound call's I/O
    /// scope (channels, identity, parent ref). Stage 7: replaces runtime1's
    /// Service-as-actor model.
    /// </summary>
    public global::app.service.list.@this Services => _services ??= new global::app.service.list.@this(this);

    /// <summary>
    /// The currently executing actor. Defaults to User. Changed to System during bootstrap (Start).
    /// app.execute switches temporarily for context-crossing dispatch.
    /// </summary>
    public actor.@this CurrentActor { get; set; } = null!; // initialized to User in constructor

    /// <summary>
    /// Resolves an actor by name. Returns error instead of null — object reports its own errors.
    /// </summary>
    public (actor.@this? Actor, IError? Error) GetActor(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return (null, new ActionError("Actor name is required", "ActorRequired", 400));

        var actor = name.ToLowerInvariant() switch
        {
            "system" => System,
            "user" => User,
            _ => (actor.@this?)null
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
    public keepalive.@this KeepAlive { get; } = new();

    /// <summary>
    /// App-wide call tree. Structural data (Action / Caller / Errors)
    /// is always captured; richer capture (timing, tags, history) is gated by
    /// <see cref="callstack.@this.Flags"/>, populated via Debug.Apply from
    /// <c>--debug={callstack:{...}}</c>.
    /// </summary>
    public callstack.@this CallStack { get; } = new();

    public @this(string absolutePath, global::app.module.@this? modules = null,
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
        Event = new global::app.@event.list.@this();
        Debug = new Debugging(this);
        Tester = new global::app.tester.@this(this);
        Builder = new global::app.module.builder.@this(this);
        Type = new type.catalog.@this();
        Config = new config.@this();
        _settingsStore = new Lazy<global::app.module.settings.IStore>(CreateSettingsStore);
        Settings = new global::app.module.settings.@this(this);
        _modules = modules ?? new global::app.module.@this();
        _modules.App = this;
        _goals = new global::app.goal.list.@this { App = this };

        Error = new global::app.error.list.@this(this);

        Code.RegisterDefaults();
        Type.RegisterDomainTypes();
        Type.Scheme.Register("file", (raw, context) => global::app.type.path.file.@this.Resolve(raw, context));
        Type.Scheme.Register("http", (raw, context) => global::app.type.path.http.@this.Resolve(raw, context));
        Type.Scheme.Register("https", (raw, context) => global::app.type.path.http.@this.Resolve(raw, context));
        Navigator.RegisterDefaults();

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
    public static void WireDefaultConsoleChannels(global::app.actor.@this actor)
    {
        if (!actor.Channel.Contains(global::app.channel.list.@this.Output))
            actor.Channel.Register(new global::app.channel.type.stream.@this(
                global::app.channel.list.@this.Output, Console.OpenStandardOutput(),
                global::app.channel.ChannelDirection.Output, ownsStream: false));
        if (!actor.Channel.Contains(global::app.channel.list.@this.Error))
            actor.Channel.Register(new global::app.channel.type.stream.@this(
                global::app.channel.list.@this.Error, Console.OpenStandardError(),
                global::app.channel.ChannelDirection.Output, ownsStream: false));
        if (!actor.Channel.Contains(global::app.channel.list.@this.Input))
            actor.Channel.Register(new global::app.channel.type.stream.@this(
                global::app.channel.list.@this.Input, Console.OpenStandardInput(),
                global::app.channel.ChannelDirection.Input, ownsStream: false));
    }

    /// <summary>
    /// Loads app identity from .build/app.pr. Called at startup.
    /// If no app.pr exists, the app keeps its generated Id.
    /// </summary>
    public async Task Load()
    {
        var prPath = global::app.type.path.@this.Resolve("/.build/app.pr", System.Context!);
        var exists = await prPath.ExistsAsync();
        if (!exists.Success || (await exists.Value())?.Value != true) return;
        var readResult = await prPath.ReadText();
        if (!readResult.Success) return;
        var json = (await readResult.Value() as global::app.type.text.@this)?.Clr<string>();
        // .pr deserialized to Goal via FilePath.ReadText's MIME path — fall back
        // to the raw text by reading directly through ReadBytes when .pr's MIME
        // converted the JSON to a typed object. For app.pr we only need the
        // identity fields.
        if (string.IsNullOrWhiteSpace(json))
        {
            var bytes = await prPath.ReadBytes();
            if (!bytes.Success || bytes.Peek() == null) return;
            json = global::System.Text.Encoding.UTF8.GetString((await bytes.Value())!.Clr<byte[]>()!);
        }
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
        Converters = {
            new global::app.channel.serializer.TimeSpanIso8601(),
            new global::app.channel.serializer.json.Converter()
        }
    };

    public async Task<data.@this> Save()
    {
        Updated = DateTime.UtcNow;
        if (Created == default) Created = Updated;
        var json = JsonSerializer.Serialize(
            new { id = Id, name = Name, created = Created, updated = Updated, version = Version },
            CamelCaseIndented);
        var prPath = global::app.type.path.@this.Resolve("/.build/app.pr", System.Context!);
        var written = await prPath.WriteText(json);
        if (!written.Success) return written;
        return app.data.@this.Ok(this);
    }

    /// <summary>
    /// Loads the PLang runtime via file.read on first use.
    /// file.read handles .pr → List&lt;Goal&gt; deserialization via MIME type mapping.
    // --- [Method] primitives — the kernel ---

    // App.Run collapsed into Action.@this.RunAsync in stage 2a.5
    // (action owns its execution — no shared App-level dispatch).
    //
    // RunAction retained as the inline-C#-composition entry: callers construct
    // a handler instance (`new sign { ... }`), this wraps it in an Action.@this
    // entity with PreboundHandler set so the entity's DispatchAsync uses the
    // pre-built handler instead of resolving via Modules.GetCodeGenerated.
    // The entity is Synthetic=true by default — wire-serialize filters such
    // frames per stage 2a.5 deliverable 4e.

    /// <summary>
    /// Runs a strongly-typed action handler through the production execution
    /// path (Push / Anchor / lifecycle / dispatch). Used by C# composing
    /// actions (providers, tests). Spec-deferred follow-up: this overload may
    /// be removed entirely when handlers grow their own RunAsync surface.
    /// </summary>
    public Task<data.@this> RunAction<TAction>(TAction handler, actor.context.@this context)
        where TAction : module.ICodeGenerated
    {
        var entity = new global::app.goal.steps.step.actions.action.@this
        {
            Module = ResolveModuleName(typeof(TAction)),
            ActionName = ResolveActionName(typeof(TAction)),
            PreboundHandler = handler,
        };
        return entity.RunAsync(context);
    }

    /// <summary>
    /// Typed variant — same dispatch path, casts the result Value to TResult.
    /// </summary>
    public async Task<data.@this<TResult>> RunAction<TAction, TResult>(TAction handler, actor.context.@this context)
        where TAction : module.ICodeGenerated
        where TResult : global::app.type.item.@this, global::app.type.item.ICreate<TResult>
    {
        var result = await RunAction(handler, context);
        if (!result.Success) return data.@this<TResult>.FromError(result.Error!);
        return data.@this<TResult>.Ok((TResult)(await result.Value())!);
    }

    private static string ResolveModuleName(System.Type handlerType)
    {
        var ns = handlerType.Namespace ?? "";
        var lastDot = ns.LastIndexOf('.');
        return lastDot >= 0 ? ns[(lastDot + 1)..] : ns;
    }

    private static string ResolveActionName(System.Type handlerType)
    {
        var attr = handlerType.GetCustomAttribute<module.ActionAttribute>(inherit: false);
        return attr?.Name ?? handlerType.Name.ToLowerInvariant();
    }

    /// <summary>
    /// Bootstrap: loads app identity, resolves the goal file, runs it.
    /// Building is routed to the PLang builder (system/builder/).
    /// </summary>
    public async Task<data.@this> Start(actor.context.@this? context = null)
    {
        await Load();

        // Invariant: every I/O actor must have all three role-channels registered
        // by the entry point before goal execution. Surface a clear error otherwise.
        foreach (var actor in new[] { System, User })
        {
            var invariant = actor.Channel.Verify();
            if (!invariant.Success) return invariant;
        }

        context ??= System.Context;
        CurrentActor = System;

        // Build → PLang builder (runs as User — user is building their code)
        if (Builder.IsEnabled) return await Builder.RunAsync();

        // Resolve goal file
        var goalFile = (await context.Variable.GetValue("goalFile")) as string;
        if (string.IsNullOrEmpty(goalFile))
            return app.data.@this.FromError(new global::app.error.ServiceError(
                "No goal file specified. Use: plang <goalfile>", "NoGoalFile", 400));

        var goalCall = new GoalCall { PrPath = global::app.type.path.@this.Resolve(goalFile, context) };
        var goalResult = await goalCall.GetGoalAsync(this, context);
        if (!goalResult.Success) return goalResult;

        var goal = (Goal)(await goalResult.Value())!;

        // Switch to user actor for user code execution
        CurrentActor = User;
        return await goal.RunAsync(User.Context);
    }

    /// <summary>
    /// Runs a goal via GoalCall. Resolves the goal then delegates to Goal.RunAsync.
    /// </summary>
    // Returns bare Data — the catalog renders this as `→ returns data`
    // (polymorphic; called goal can return any value).
    public async Task<data.@this> RunGoalAsync(GoalCall goalCall, actor.context.@this? context = null, CancellationToken ct = default)
    {
        context ??= User.Context;
        var goalResult = await goalCall.GetGoalAsync(this, context);
        if (!goalResult.Success) return goalResult;

        // Inject parameters — GetGoalAsync only injects when loading from file,
        // but goals found in memory (app.goal.Get) need parameters too.
        // Goal-call is *not* a fork: it stays in the caller's flow. Variables.Set
        // is overlay-aware — if a fork operator above us (channel fire, parallel
        // foreach iteration) pushed a Calls scope, these writes land in that
        // scope; otherwise they go to the actor-shared dict. Either way,
        // sequential goal.call shares state with its caller (LoadUser leak still
        // works in plain top-of-flow code), and concurrent invocations are
        // isolated by whatever forked them.
        if (goalCall.Parameters != null)
            foreach (var param in goalCall.Parameters)
            {
                // Call-by-value at the boundary: the param rides raw (`%user%`) from the
                // .pr; this Set IS the assignment, and assignment evaluates once — decode
                // in the caller's scope (full-match `%var%` yields the live variable Data,
                // type intact; literals pass through), then store under the param name.
                param.Context = context;
                await context.Variable.Set(param.Name, await param.AsCanonical(context));
            }

        return await ((Goal)(await goalResult.Value())!).RunAsync(context);
    }

    /// <summary>
    /// Runs a goal already in memory. Delegates to Goal.RunAsync.
    /// </summary>
    public async Task<data.@this> RunGoalAsync(Goal goal, actor.context.@this? context = null, CancellationToken ct = default)
    {
        context ??= User.Context;
        return await goal.RunAsync(context);
    }

    private global::app.module.settings.IStore CreateSettingsStore()
    {
        // Testing: in-memory db scoped by App.Id so per-test Apps never share state.
        // SQLite's shared-cache merges in-memory dbs with identical DataSource names,
        // so the App.Id scoping is load-bearing.
        if (Tester.IsEnabled)
            return global::app.module.settings.Sqlite.InMemory($"system-{Id}");

        // Lift to Path: AuthGate fires inside the Sqlite ctor on Write,
        // parent dir creation via path.Mkdir.
        var dbPath = global::app.type.path.@this.Resolve("/.db/system.sqlite", System.Context!);
        return new global::app.module.settings.Sqlite(dbPath);
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
