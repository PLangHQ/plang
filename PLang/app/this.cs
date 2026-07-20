using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Reflection;
using app.actor.context;
using app.module.action.setting;
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
    private readonly global::app.module.list.@this _modules;
    private readonly global::app.goal.list.@this _goals;
    private bool _disposed;

    private readonly actor.@this _system;
    private readonly actor.@this _user;
    private global::app.service.list.@this? _services;

    /// <summary>
    /// Unique identifier for this app. Loaded from app.pr, or generated on first run.
    /// </summary>
    [global::app.Store]
    public string Id { get; internal set; }

    /// <summary>
    /// Name of this app.
    /// </summary>
    [global::app.Store]
    public string Name { get; internal set; }

    /// <summary>
    /// When the app was first created.
    /// </summary>
    [global::app.Store]
    public DateTime Created { get; internal set; }

    /// <summary>
    /// When the app was last updated.
    /// </summary>
    [global::app.Store]
    public DateTime Updated { get; internal set; }

    /// <summary>
    /// Version of the builder used.
    /// </summary>
    [global::app.Store]
    public string? Version { get; internal set; }

    /// <summary>
    /// The OS absolute path of the application (e.g. C:\myapp or /home/user/app).
    /// </summary>
    public string AbsolutePath { get; }

    /// <summary>
    /// The OS absolute path to the os/ folder (next to the plang executable).
    /// System goals (builder, events, etc.) are resolved from os/system/... here.
    /// Null when no os directory is configured.
    /// </summary>
    public string? OsDirectory { get; internal set; }

    /// <summary>
    /// Parent app, when this app was constructed as a child of another.
    /// A child inherits its parent's filesystem scope: <c>path.@this.IsInRoot</c>
    /// walks the Parent chain, so a child app rooted at a narrower
    /// subdirectory still treats the parent's <c>AbsolutePath</c> as in-root.
    /// Null for top-level apps.
    /// </summary>
    public app.@this? Parent { get; internal set; }

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
    public CultureInfo Culture { get; internal set; } = CultureInfo.InvariantCulture;

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
    public global::app.module.list.@this Module => _modules;

    /// <summary>
    /// Type-keyed provider registry for pluggable module implementations.
    /// Modules define provider interfaces, register defaults, PLang developers override via DLL.
    /// </summary>
    public AppCode Code { get; }

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
    public ICache Cache { get; internal set; } = new global::app.module.action.cache.Memory();

    /// <summary>
    /// App-level persistent key-value store backed by <c>.db/system.sqlite</c>
    /// (or in-memory when Test is active). One per app — actors share it.
    /// Modules own their tables (<c>encryption</c>, <c>settings</c>, <c>llm-cache</c>, etc.).
    /// Created lazily on first access so tests with fictional paths and apps
    /// that never touch settings don't pay for SQLite-file creation at boot.
    /// </summary>
    public Task<global::app.module.action.setting.IStore> SettingsStore => _settingsStore.Value;
    private Lazy<Task<global::app.module.action.setting.IStore>> _settingsStore = null!;

    /// <summary>
    /// The app-level setting authority (chain root, <c>_parent == null</c>). Holds both lifetimes
    /// behind <c>Storage</c>: in-memory (this-run cascade, CLI <c>--flags</c>) and persistent
    /// (sqlite, <c>%setting.X%</c>, via <see cref="SettingsStore"/>). Every context's
    /// <c>Setting</c> chains up to this one.
    /// </summary>
    public global::app.setting.@this Setting { get; }

    /// <summary>
    /// Debug mode controller. null = off; non-null = on (born under --debug).
    /// Presence is the enable signal — there is no IsEnabled.
    /// </summary>
    public Debug? Debug { get; set; }

    /// <summary>
    /// Run-wide error scope. AsyncLocal-flowed current error (PLang <c>%!error%</c>) +
    /// audit list of every error pushed. Populated by error.handle.Wrap during recovery.
    /// </summary>
    public global::app.error.list.@this Error { get; }

    /// <summary>
    /// Test session — the collection of discovered/run *.test.goal tests plus
    /// run-wide state. null = not testing; non-null = a live session (born under --test).
    /// </summary>
    public global::app.test.list.@this? Test { get; set; }

    /// <summary>
    /// Build mode controller. null = off; non-null = on (born under --build).
    /// When present, actors use in-memory datasources.
    /// </summary>
    public global::app.module.action.build.@this? Build { get; set; }

    /// <summary>
    /// Allow creating a new app if none exists. Set via --app={"create":true}. Default false.
    /// </summary>
    public bool Create { get; set; }

    /// <summary>
    /// Centralized type identity: PLang names ↔ CLR types. File-format
    /// characteristics live on <see cref="Format"/>.
    /// </summary>
    public type.list.@this Type { get; }

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
    public actor.@this System => _system;

    /// <summary>
    /// User actor for end user operations. Links to System's cancellation token.
    /// </summary>
    public actor.@this User => _user;

    /// <summary>
    /// Flat per-call Service collection. Each Service is one outbound call's I/O
    /// scope (channels, identity, parent ref). Stage 7: replaces runtime1's
    /// Service-as-actor model.
    /// </summary>
    public global::app.service.list.@this Services => _services ??= new global::app.service.list.@this(this);


    /// <summary>
    /// Resolves an actor by name. The actor set is closed and hardcoded
    /// (system/user), so an unknown name is a critical miss, not a soft one —
    /// throws and returns a non-null actor. The conversion pipeline wraps the
    /// throw into a graceful error for untrusted (LLM) input.
    /// </summary>
    public actor.@this GetActor(string name)
        => name?.ToLowerInvariant() switch
        {
            "system" => System,
            "user" => User,
            _ => throw new ArgumentException(
                $"Unknown actor '{name}' — the actor set is closed (system/user).", nameof(name))
        };

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

    public @this(string absolutePath, global::app.module.list.@this? modules = null,
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

        // Context is fundamental — it is born before almost everything else. The
        // system & user actors (each owning a long-lived context) are constructed
        // first so Type, Code, and the rest can be handed the context they birth
        // values from. System is the cancellation root; User links to its token.
        // The actor/context ctor touches App only lazily (Settings/Code via deferred
        // lambdas) and uses pure-static type seeds, so nothing here needs Type/Code yet.
        _system = new actor.@this("System", this, _shutdownCts.Token);
        _user = new actor.@this("User", this, _system.CancellationToken);

        Event = new global::app.@event.list.@this();
        // Debug/Test/Build are born on their flag (--debug/--test/--build), not at
        // startup — null = off. Presence is the enable signal (no IsEnabled).
        Type = new type.list.@this(System.Context);
        Code = new AppCode(System.Context);
        _settingsStore = new Lazy<Task<global::app.module.action.setting.IStore>>(CreateSettingsStoreAsync);
        Setting = new global::app.setting.@this(System.Context);
        _modules = modules ?? new global::app.module.list.@this();
        _modules.App = this;
        _goals = new global::app.goal.list.@this { App = this };

        Error = new global::app.error.list.@this(this);

        Code.RegisterDefaults();
        Type.RegisterDomainTypes();
        // Closed sets (choice<T>: operator, httpmethod, …) surface only through handler params
        // and enum choices carry no [Choices] marker, so the choice registry discovers them by
        // scanning the assembly's choice<T> usages — reverse-resolvable ("operator" → choice<T>)
        // + readable. code.load re-runs this for a late-loaded assembly (Discover below).
        Type.Choice.Register(typeof(global::app.@this).Assembly);
        Type.Scheme.Register("file", (raw, context) => global::app.type.item.path.file.@this.Resolve(raw, context));
        Type.Scheme.Register("http", (raw, context) => global::app.type.item.path.http.@this.Resolve(raw, context));
        Type.Scheme.Register("https", (raw, context) => global::app.type.item.path.http.@this.Resolve(raw, context));

        // Auto-wire console channels for ad-hoc App constructions (sub-process
        // test fixtures, embedded scenarios, C# tests, the `plang --test` child
        // app). These are NOT the interactive terminal owner, so their input is
        // a non-blocking EOF sink — a prompt fails fast with ChannelEof instead
        // of reading the shared process stdin (which deadlocks under parallel
        // tests). Ask I/O goes through channels: a caller that wants to answer
        // registers its own input/ask channel (e.g. tests' CannedAnswerChannel).
        // The one interactive owner — the CLI (Executor) — constructs with
        // autoWireConsoleChannels:false and calls WireDefaultConsoleChannels
        // itself to bind real stdin.
        if (autoWireConsoleChannels)
        {
            WireConsoleChannels(System, interactiveInput: false);
            WireConsoleChannels(User, interactiveInput: false);
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
    /// the well-known names ("output", "error", "input"), with real interactive
    /// stdin as the input source. The interactive CLI (Executor) calls this for
    /// System and User after constructing the App with autoWireConsoleChannels:false.
    /// Non-interactive constructions (tests, embedded, the test runner's child
    /// app) get the EOF-sink input via the ctor's auto-wire instead.
    /// </summary>
    public static void WireDefaultConsoleChannels(global::app.actor.@this actor)
        => WireConsoleChannels(actor, interactiveInput: true);

    /// <summary>
    /// Wires output/error to the console standard streams. The input channel
    /// reads real stdin when <paramref name="interactiveInput"/> is true (the
    /// one terminal owner — the CLI); otherwise it binds <see cref="System.IO.Stream.Null"/>,
    /// a non-blocking EOF source, so a prompt with no registered answerer fails
    /// fast with ChannelEof rather than blocking on the shared process stdin.
    /// </summary>
    public static void WireConsoleChannels(global::app.actor.@this actor, bool interactiveInput)
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
            actor.Channel.Register(interactiveInput
                // The one terminal owner (CLI) reads real stdin.
                ? new global::app.channel.type.stream.@this(
                    global::app.channel.list.@this.Input, Console.OpenStandardInput(),
                    global::app.channel.ChannelDirection.Input, ownsStream: false)
                // Non-interactive: an empty in-memory stream — reads as instant
                // EOF (ChannelEof), so a prompt with no registered answerer fails
                // fast instead of blocking on the shared process stdin.
                : global::app.channel.type.stream.@this.Memory(
                    global::app.channel.list.@this.Input,
                    global::app.channel.ChannelDirection.Input));
    }

    /// <summary>
    /// Loads app identity from .build/app.pr. Called at startup.
    /// If no app.pr exists, the app keeps its generated Id.
    /// </summary>
    public async Task Load()
    {
        var prPath = global::app.type.item.path.@this.Resolve("/.build/app.pr", System.Context!);
        var exists = await prPath.ExistsAsync();
        if (!exists.Success || (await exists.Value())?.Value != true) return;
        var readResult = await prPath.ReadText();
        if (!readResult.Success) return;
        var json = (await readResult.Value() as global::app.type.item.text.@this)?.Clr<string>();
        // .pr deserialized to Goal via FilePath.ReadText's MIME path — fall back
        // to the raw text by reading directly through ReadBytes when .pr's MIME
        // converted the JSON to a typed object. For app.pr we only need the
        // identity fields.
        if (string.IsNullOrWhiteSpace(json))
        {
            var bytes = await prPath.ReadBytes();
            if (!bytes.Success || bytes.Peek().IsNull) return;
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
    public async Task<data.@this> Save()
    {
        Updated = DateTime.UtcNow;
        if (Created == default) Created = Updated;
        // App writes its own app.pr through the SAME door a goal writes its .pr: wrap the host in
        // a clr carrier and let the serializer reflect its [Store] face — indented, Store view. No
        // hard-coded field list (add a [Store] prop → it persists), no json.Writer, no options bag;
        // indentation and format are the serializer's, not App's.
        var serializer = (global::app.channel.serializer.plang.@this)
            System.Context!.Actor!.Channel.Serializers.GetOrDefault("application/plang");
        using var ms = new global::System.IO.MemoryStream();
        await serializer.SerializeItemAsync(ms,
            new global::app.type.clr.@this<global::app.@this>(this, System.Context!), global::app.View.Store);
        var prPath = global::app.type.item.path.@this.Resolve("/.build/app.pr", System.Context!);
        var written = await prPath.WriteText(global::System.Text.Encoding.UTF8.GetString(ms.ToArray()));
        if (!written.Success) return written;
        return System.Context!.Ok(this);
    }

    /// <summary>
    /// Loads the PLang runtime via file.read on first use.
    /// file.read handles .pr → List&lt;Goal&gt; deserialization via MIME type mapping.
    // --- [Method] primitives — the kernel ---

    // App.Run collapsed into Action.@this.RunAsync in stage 2a.5
    // (action owns its execution — no shared App-level dispatch).
    //
    // Run retained as the inline-C#-composition entry: callers construct
    // a handler instance (`new sign { ... }`), this wraps it in an Action.@this
    // entity with Seed set so the generated Resolve passes through the composed
    // action's set params and fills the unset ones from setting → [Default].
    // The entity is Synthetic=true by default — wire-serialize filters such
    // frames per stage 2a.5 deliverable 4e.

    /// <summary>
    /// Runs a strongly-typed action handler through the production execution
    /// path (Push / Anchor / lifecycle / dispatch). Used by C# composing
    /// actions (providers, tests). Spec-deferred follow-up: this overload may
    /// be removed entirely when handlers grow their own RunAsync surface.
    /// </summary>
    public Task<data.@this> Run<TAction>(TAction handler, actor.context.@this context)
        where TAction : module.ICodeGenerated
    {
        var entity = new global::app.goal.step.action.@this
        {
            Module = ResolveModuleName(typeof(TAction)),
            ActionName = ResolveActionName(typeof(TAction)),
            Seed = handler,
        };
        return entity.RunAsync(context);
    }

    /// <summary>
    /// Typed variant — same dispatch path, casts the result Value to TResult.
    /// </summary>
    public async Task<data.@this<TResult>> Run<TAction, TResult>(TAction handler, actor.context.@this context)
        where TAction : module.ICodeGenerated
        where TResult : global::app.type.item.@this, global::app.type.item.ICreate<TResult>
    {
        var result = await Run(handler, context);
        if (!result.Success) return context.Error<TResult>(result.Error!);
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
    public async Task<data.@this> Start()
    {
        await Load();

        // Invariant: every I/O actor must have all three role-channels registered
        // by the entry point before goal execution. Surface a clear error otherwise.
        foreach (var actor in new[] { System, User })
        {
            var invariant = actor.Channel.Verify();
            if (!invariant.Success) return invariant;
        }

        // Bootstrap runs under System's context; user code runs under User's context (below).
        // Execution flows the actor via its context — there is no global "current actor".
        var context = System.Context;

        // Build → PLang builder (runs as User — user is building their code).
        // Presence is the enable signal (staged: one owned check; full dissolve to
        // entry-action-at-birth is a follow-up branch, plan §6.C).
        if (Build != null) return await Build.RunAsync();

        // Resolve goal file
        var goalFile = await (await context.Variable.Get("goalFile")).Clr<string?>(null);
        if (string.IsNullOrEmpty(goalFile))
            return context.Error(new global::app.error.ServiceError(
                "No goal file specified. Use: plang <goalfile>", "NoGoalFile", 400));

        var goalCall = new GoalCall { PrPath = global::app.type.item.path.@this.Resolve(goalFile, context) };
        var goalResult = await goalCall.GetGoalAsync(this, context);
        if (!goalResult.Success) return goalResult;

        var goal = ((await goalResult.Value()) as Goal)!;

        // User code executes under the User actor's context.
        return await goal.RunAsync(User.Context);
    }

    /// <summary>
    /// Runs a goal via GoalCall. Resolves the goal then delegates to Goal.RunAsync.
    /// </summary>
    // Returns bare Data — the catalog renders this as `→ returns data`
    // (polymorphic; called goal can return any value).
    public async Task<data.@this> RunGoalAsync(GoalCall goalCall, actor.context.@this context, CancellationToken ct = default)
    {
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
                // Data just flows — bind each arg's Data under its name as-is, no inspection,
                // no resolve. It resolves/renders on its own door when the callee reads it.
                // A self-reference arg (`call Foo x=%x%`) is dropped at build (goal.call.Build),
                // never handled here.
                param.Context = context;
                await context.Variable.Set(param.Name, param);
            }

        return await ((await goalResult.Value()) as Goal)!.RunAsync(context);
    }

    /// <summary>
    /// Runs a goal already in memory. Delegates to Goal.RunAsync.
    /// </summary>
    public async Task<data.@this> RunGoalAsync(Goal goal, actor.context.@this context, CancellationToken ct = default)
    {
        return await goal.RunAsync(context);
    }

    private async Task<global::app.module.action.setting.IStore> CreateSettingsStoreAsync()
    {
        // Testing: in-memory db scoped by App.Id so per-test Apps never share state.
        // SQLite's shared-cache merges in-memory dbs with identical DataSource names,
        // so the App.Id scoping is load-bearing.
        if (Test != null)
            return global::app.module.action.setting.Sqlite.InMemory($"system-{Id}", System.Context);

        // Lift to Path: AuthGate fires inside Sqlite.CreateAsync on Write,
        // parent dir creation via path.Mkdir. Async all the way — no sync-wait,
        // so parallel App constructions never starve the threadpool.
        var dbPath = global::app.type.item.path.@this.Resolve("/.db/system.sqlite", System.Context);
        return await global::app.module.action.setting.Sqlite.CreateAsync(dbPath, System.Context);
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
