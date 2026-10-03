using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Reflection;
using app.actor.context;
using app.module.setting;
using app.error;
using app.module;
using app.Utils;
using Goal = app.goal.@this;

namespace app;

/// <summary>
/// Main runtime for PLang App.
/// Executes goals and manages the execution lifecycle.
/// Self-contained: owns all app-level state (environment, culture, shutdown, key-value store).
/// </summary>
public sealed partial class @this : global::app.type.item.@this, IAsyncDisposable, global::app.type.item.setting.ISetting<global::app.setting.@this>
{
    /// <summary>A structure — written through the reflection kind, its [Out]/[Debug] members.</summary>
    public override bool IsLeaf => false;

    // what a program adds to the app (%!app.home%) — kept for the app's life, and in its snapshot
    private readonly global::app.type.item.kept.list.@this _kept = new();
    internal override global::app.type.item.kept.list.@this Kept => _kept;

    /// <summary>The one root: a copy of the app is the app.</summary>
    protected internal override global::app.type.item.@this Clone() => this;

    private readonly CancellationTokenSource _shutdownCts = new();
    private bool _disposed;

    private global::app.service.list.@this? _services;

    /// <summary>
    /// Unique identifier for this app. Loaded from app.pr, or generated on first run.
    /// </summary>
    [global::app.Store]
    public string Id { get; internal set; }

    /// <summary>
    /// The app's name as <paramref name="context"/>'s settings have it (<c>%!app.setting.name%</c>), else its
    /// folder's name — <c>%!app.name%</c> answers as its asker.
    /// </summary>
    [global::app.LlmBuilder]
    public global::app.type.item.text.@this Name(global::app.actor.context.@this context)
        => context.Setting.Of<global::app.setting.@this>().Name ?? _folder;

    // The app's folder's name — what the app goes by until a setting names it.
    private readonly string _folder;

    /// <summary>
    /// When the app was first created — its identity's, else this run's start (an app with no identity yet is
    /// being created now).
    /// </summary>
    [global::app.Store]
    public global::app.type.item.datetime.@this Created { get; internal set; }

    /// <summary>
    /// When the app's identity last changed — read from app.pr, else this run's start (an identity created now).
    /// </summary>
    [global::app.Store]
    public global::app.type.item.datetime.@this Updated { get; internal set; }

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
    /// Parent app, when this app was constructed as a child of another.
    /// A child inherits its parent's filesystem scope: <c>path.@this.IsInRoot</c>
    /// walks the Parent chain, so a child app rooted at a narrower
    /// subdirectory still treats the parent's <c>AbsolutePath</c> as in-root.
    /// Null for top-level apps.
    /// </summary>
    public app.@this? Parent { get; }

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
    /// The environment the app runs in ("production", "development") as <paramref name="context"/>'s settings
    /// have it (<c>%!app.setting.environment%</c>) — <c>%!app.environment%</c> answers as its asker.
    /// </summary>
    [global::app.LlmBuilder]
    public global::app.type.item.text.@this Environment(global::app.actor.context.@this context)
        => context.Setting.Of<global::app.setting.@this>().Environment;

    /// <summary>The calls of <paramref name="context"/> — <c>%!app.call%</c> answers as its asker.</summary>
    [global::app.LlmBuilder]
    public global::app.call.list.@this call(global::app.actor.context.@this context) => context.call;

    /// <summary>The trace of <paramref name="context"/>'s run — <c>%!app.trace%</c> answers as its asker.</summary>
    [global::app.LlmBuilder]
    public global::app.actor.context.trace.@this trace(global::app.actor.context.@this context) => context.Trace;

    /// <summary>What the asker's last action answered — its memory's <c>!data</c>; <c>%!app.data%</c> answers as its
    /// asker.</summary>
    [global::app.LlmBuilder]
    public global::app.data.@this? data(global::app.actor.context.@this context) => context.Variable.Peek("!data");

    /// <summary>The event in play for <paramref name="context"/> — <c>%!app.event%</c> answers as its asker: the running
    /// bound call's Data, its value the event, its properties what it fired for (<c>%!app.event!item%</c>) and the
    /// result so far (<c>%!app.event!result%</c>). Unset outside one.</summary>
    [global::app.LlmBuilder]
    public global::app.data.@this @event(global::app.actor.context.@this context)
        => context.call.Event ?? context.NotFound("event");

    /// <summary>
    /// When the app was started.
    /// </summary>
    public global::app.type.item.datetime.@this StartedAt { get; }

    /// <summary>
    /// How long the app has been running.
    /// </summary>
    public global::app.type.item.duration.@this Uptime => new(DateTimeOffset.UtcNow - StartedAt.Value);

    /// <summary>
    /// Cancellation token for graceful shutdown — a handle, never written with the app.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public CancellationToken ShutdownToken => _shutdownCts.Token;

    /// <summary>
    /// The app's module — <c>%!app.module%</c>: an empty module whose <c>list</c> is every module the app loads,
    /// registering their actions; <c>Get(name)</c> is one module as a result.
    /// </summary>
    public global::app.module.@this module { get; }

    /// <summary>
    /// Type-keyed provider registry for pluggable module implementations.
    /// Modules define provider interfaces, register defaults, PLang developers override via DLL.
    /// </summary>
    public AppCode Code { get; }

    /// <summary>
    /// The type named <c>goal</c> — <c>%!app.goal%</c>: its <c>list</c> is the goals read so far (and the
    /// reading: every goal through <c>All()</c>, the goal a call names through <c>Find</c>),
    /// <c>Get(address)</c> is one goal as a result, <c>current</c> the running one.
    /// </summary>
    public global::app.type.current.@this<Goal, global::app.goal.list.@this> goal { get; }

    /// <summary>
    /// The disk — what a file path's verbs reach once their gate has passed. A context reads and writes
    /// through its own (<c>context.FileSystem</c>): this one, or the overlay a build checks its goals over.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public global::app.type.item.path.file.filesystem.@this FileSystem { get; } = new();

    /// <summary>
    /// Pluggable step cache. Default: in-memory. Swap via: - use 'redis.dll' for caching
    /// </summary>
    public ICache Cache { get; internal set; } = new global::app.module.cache.Memory();

    /// <summary>
    /// The app's store — <c>.data/data.sqlite</c> (in memory while testing, under this app's id). One per
    /// app — actors share it; its owners keep their tables (<c>settings</c>, setup's steps, the LLM cache, …).
    /// Born with the app and opened at its first verb, so an app that never touches it pays for no SQLite file.
    /// </summary>
    public global::app.store.@this store { get; }

    /// <summary>
    /// Debug mode controller. null = off; non-null = on (born under --debug).
    /// Presence is the enable signal — there is no IsEnabled.
    /// </summary>
    public Debug? Debug { get; set; }

    /// <summary>
    /// The type named <c>test</c> — <c>%!app.test%</c>: its <c>list</c> is the run's tests (with the run's
    /// setting, report and session), <c>Get(address)</c> is one test as a result, <c>current</c> the test the
    /// asker is running. The app is testing while the list's session is open.
    /// </summary>
    public global::app.type.current.@this<global::app.test.@this, global::app.test.list.@this> test { get; }

    /// <summary>
    /// The type named <c>error</c> — <c>%!app.error%</c>: <c>current</c> is the error in play (the asker's call
    /// stack's, not yet handled), <c>list</c> every error on the asker's call stack, handled or not.
    /// </summary>
    public global::app.type.current.@this<global::app.error.Error, global::app.error.list.@this> error { get; }

    /// <summary>
    /// The type named <c>variable</c> — <c>%!app.variable%</c>: its <c>list</c> is the asker's memory
    /// (navigation's context), <c>.user</c> one variable in it. C# has no asker here: it uses
    /// <c>context.Variable</c>, and <c>app.variable.list</c> says so.
    /// </summary>
    public global::app.type.@this<global::app.type.item.variable.@this, global::app.type.item.list.@this<global::app.type.item.variable.@this>> variable { get; }

    /// <summary>
    /// The type named <c>shortcut</c> — <c>%!app.shortcut%</c>: its <c>list</c> is the app's shortcuts, each a
    /// goal under a <c>shortcut/</c> folder named by its file, read once at the first ask. <c>%!goal%</c> reads
    /// the shortcut named <c>goal</c> when there is one, before the app's member of that name.
    /// </summary>
    public global::app.type.@this<global::app.shortcut.@this, global::app.shortcut.list.@this> shortcut { get; }

    /// <summary>
    /// Build mode controller. null = off; non-null = on (born under --build).
    /// When present, actors use in-memory datasources.
    /// </summary>
    public global::app.module.build.@this? Build { get; set; }

    /// <summary>What this App is doing — derived from what it holds: building when it has a Build,
    /// testing while its test session is open, otherwise running. Not a stored field, so there is no
    /// second truth; the App branches on this, never on the presence itself.</summary>
    public global::app.type.item.choice.@this<global::app.Mode> Mode
        => Build != null ? global::app.Mode.Build
         : test.list.Session != null ? global::app.Mode.Test
         : global::app.Mode.Run;

    /// <summary>
    /// The type named <c>type</c> — <c>%!app.type%</c>: its <c>list</c> is the app's types (the
    /// lookups by name, C# class, identity, MIME and extension, and the kinds), <c>Get(name)</c> is
    /// one type as a result. A file format is a kind of the type that reads it.
    /// </summary>
    public global::app.type.@this<global::app.type.@this, global::app.type.list.@this> type { get; }

    /// <summary>
    /// The type named <c>actor</c> — <c>%!app.actor%</c>: its <c>list</c> is the app's two actors, System
    /// and User; <c>Get(name)</c> is one as a result, <c>current</c> the one the asker acts as.
    /// </summary>
    public global::app.type.current.@this<global::app.actor.@this, global::app.actor.list.@this> actor { get; }

    // The types a step and an action start through, and a channel writes, reads and asks through — the type
    // list's own entries, held because every start and every channel write reaches their events and a walk of
    // the list copies it. Not a second store: a type's name can't be registered twice, so these can't drift.
    internal global::app.type.@this step { get; }
    internal global::app.type.@this action { get; }
    internal global::app.type.@this channel { get; }

    /// <summary>
    /// Flat per-call Service collection. Each Service is one outbound call's I/O
    /// scope (channels, identity, parent ref). Stage 7: replaces runtime1's
    /// Service-as-actor model.
    /// </summary>
    public global::app.service.list.@this Services => _services ??= new global::app.service.list.@this(this);


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

    /// <summary>A child of <paramref name="parent"/> — rooted at the parent's root, with its os/ folder
    /// (a test's App: the .pr files' root-relative paths resolve as they were built).</summary>
    public @this(@this parent) : this(parent.AbsolutePath)
    {
        Parent = parent;
    }

    public @this(string absolutePath,
        string? environment = null,
        bool autoWireConsoleChannels = true)
    {
        Id = Guid.NewGuid().ToString("N")[..12];
        var trimmed = absolutePath.TrimEnd('/', '\\');
        var lastSep = trimmed.LastIndexOfAny(['/', '\\']);
        _folder = lastSep >= 0 ? trimmed[(lastSep + 1)..] : trimmed;
        AbsolutePath = absolutePath;
        StartedAt = new(DateTimeOffset.UtcNow);
        Created = StartedAt;
        Updated = StartedAt;

        // Context is fundamental — it is born before almost everything else. The
        // system & user actors (each owning a long-lived context) are constructed
        // first so Type, Code, and the rest can be handed the context they birth
        // values from. System is the cancellation root; User links to its token.
        // The actor/context ctor touches App only lazily (Settings/Code via deferred
        // lambdas) and uses pure-static type seeds, so nothing here needs Type/Code yet.
        actor = new(this);

        // Debug/Build are born on their flag (--debug/--build), not at
        // startup — null = off. Presence is the enable signal (no IsEnabled).
        type = new(this);
        type.list.Replace(type);   // %!app.type% and the list's entry named type are one object
        Code = new AppCode(actor.list.System.Context);
        module = new(this);
        goal = new(this);
        test = new(this);
        error = new(this);
        variable = new(this);
        shortcut = new(this);
        // each concept's type is the list's entry of its name, as app.type is — %!app.goal% and the type goal
        // are one object
        type.list.Replace(goal);
        type.list.Replace(test);
        type.list.Replace(error);
        type.list.Replace(variable);
        step = type.list["step"];
        action = type.list["action"];
        channel = type.list["channel"];

        Code.RegisterDefaults();
        // path's schemes, each a kind of path that builds its own path subclass. (The types' own
        // scan finds the kind classes and choice's closed sets.)
        type.list.Add(new global::app.type.item.path.scheme.@this("file", (raw, context) => global::app.type.item.path.file.@this.Resolve(raw, context)));
        type.list.Add(new global::app.type.item.path.scheme.@this("http", (raw, context) => global::app.type.item.path.http.@this.Resolve(raw, context)));
        type.list.Add(new global::app.type.item.path.scheme.@this("https", (raw, context) => global::app.type.item.path.http.@this.Resolve(raw, context)));

        // The store: where it lives is decided when it opens — in memory while testing, scoped by this app's id
        // (per-test apps never share a database: SQLite's shared cache merges in-memory databases of one name),
        // else its file.
        // an environment given at construction is this run's value of the app's setting
        if (environment != null)
        {
            var own = new global::app.setting.@this().Path;
            _ = actor.list.System.Setting.Set(own + ".environment",
                new data.@this("environment", new global::app.type.item.text.@this(environment), context: actor.list.System.Context));
        }

        store = new global::app.store.sqlite.@this(
            global::app.type.item.path.@this.Resolve("/.data/data.sqlite", actor.list.System.Context),
            () => Mode.Value == global::app.Mode.Test ? $"system-{Id}" : null,
            actor.list.System.Context);

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
            WireConsoleChannels(actor.list.System, interactiveInput: false);
            WireConsoleChannels(actor.list.User, interactiveInput: false);
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
                global::app.channel.ChannelDirection.Output, ownsStream: false) { Framed = true });
        if (!actor.Channel.Contains(global::app.channel.list.@this.Error))
            actor.Channel.Register(new global::app.channel.type.stream.@this(
                global::app.channel.list.@this.Error, Console.OpenStandardError(),
                global::app.channel.ChannelDirection.Output, ownsStream: false) { Framed = true });
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
    /// If no app.pr exists, the app keeps its generated Id; one that can't be read is the answer.
    /// </summary>
    public async Task<data.@this> Load()
    {
        var identity = await Identity();
        if (!identity.Success) return identity;
        // the actors' saved settings, read once: after this a setting is built in memory
        await actor.list.User.Setting.Load();
        return identity;
    }

    // The app's identity, from .build/app.pr when there is one — read back through the face Save writes: the
    // content, json, taken as a dict, and the app's [Store] members read from it. An app.pr that can't be
    // read is the app's error, naming the file; the identity is left as it was.
    private async Task<data.@this> Identity()
    {
        var context = actor.list.System.Context!;
        var prPath = global::app.type.item.path.@this.Resolve("/.build/app.pr", context);
        var exists = await prPath.Exists(context);
        if (!exists.Success || (await exists.Value())?.Value != true) return context.Ok();
        // app.pr is the app's identity, not a goal: its raw content. Its value would go through the .pr
        // format's goal reader, which refuses a file that isn't a goal.
        var bytes = await prPath.Bytes(context);
        if (!bytes.Success) return Unreadable(bytes.Error?.Message);
        var raw = (await bytes.Value())?.Clr<byte[]>();
        if (raw is not { Length: > 0 } || raw.All(b => b is (byte)' ' or (byte)'\n' or (byte)'\r' or (byte)'\t'))
            return context.Ok();
        try
        {
            var decoded = await context.App.type.list.Mime("application/json").Decode(raw, context, "app.pr");
            if (await decoded.Value<global::app.type.item.dict.@this>() is not { } identity || !decoded.Success)
                return Unreadable(decoded.Error?.Message ?? "not a json object");
            new global::app.type.item.kind.reflection.@this().Read(identity, this, context);
            _stored = true;
            return context.Ok();
        }
        // an identity value its type declines (text that isn't a datetime) is an app.pr that isn't the identity
        catch (global::app.error.DeclinedException declined) { return Unreadable(declined.Error.Message); }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidCastException or InvalidOperationException)
        {
            return Unreadable(ex.Message);
        }

        data.@this Unreadable(string? why) => context.Error(new global::app.error.Error(
            $"{prPath} is not the app's identity: {why}", "AppIdentityUnreadable", 400));
    }

    // The identity is in .build/app.pr: read from it at startup, or written there since.
    private bool _stored;

    /// <summary>
    /// Saves the app's identity to .build/app.pr when it isn't there yet — the first build creates it. An identity
    /// read from app.pr is never written again because a build ran: app.pr is who the app is, not when it was built.
    /// </summary>
    public async Task<data.@this> Save()
    {
        if (_stored) return actor.list.System.Context!.Ok(this);
        // App says where; the file writes it — .pr is a program file, so its format writes the host's [Store]
        // face. No hard-coded field list (add a [Store] prop → it persists).
        var prPath = global::app.type.item.path.@this.Resolve("/.build/app.pr", actor.list.System.Context!);
        var written = await prPath.Save(actor.list.System.Context!.Ok(new global::app.type.clr.@this<global::app.@this>(this, actor.list.System.Context!)), actor.list.System.Context!);
        if (!written.Success) return written;
        _stored = true;
        return actor.list.System.Context!.Ok(this);
    }

    /// <summary>
    /// Bootstrap: loads app identity, resolves the goal file, runs it — and shows a failed run once,
    /// through <c>/system/error/Show</c>. Building is routed to the PLang builder (system/builder/).
    /// </summary>
    public async Task<data.@this> Start()
    {
        var result = await Launch();
        await Settle();
        return result.Success ? result : await Show(result);
    }

    // The run ends when its tasks do: every actor's still running, and the ones they start — cancelled with their
    // actor; a task that fails after the goal returned reports itself.
    private async Task Settle()
    {
        while (actor.list.Items().Any(each => each.Task.list.Any()))
            foreach (var each in actor.list.Items()) await each.Task.Wait();
    }

    /// <summary>
    /// Shows a failed run through the plang goal <c>/system/error/Show</c>, the error handed in as
    /// <c>%error%</c> — the run is over, so no frame holds it for <c>%!error%</c>. The failure comes
    /// back marked <c>shown</c>; if Show cannot load or itself fails, it comes back unmarked, for the
    /// process boundary to print as a last resort.
    /// </summary>
    private async Task<data.@this> Show(data.@this failed)
    {
        if (failed.Error is not { } error) return failed;
        var context = actor.list.User.Context;
        var loaded = await goal.Load("/system/error/Show.goal");
        if (!loaded.Success || await loaded.Value() is not Goal show)
        {
            await (Debug?.Write($"error show: /system/error/Show could not load — {loaded.Error}") ?? Task.CompletedTask);
            return failed;
        }

        data.@this shown;
        await using (context.call.Push(new[] { new data.@this("error", error, context: context) }))
            shown = await show.Start(context);
        if (shown.Success) failed.Properties.Set("shown", true);
        else await (Debug?.Write($"error show: /system/error/Show failed — {shown.Error}") ?? Task.CompletedTask);
        return failed;
    }

    private async Task<data.@this> Launch()
    {
        var identity = await Load();
        if (!identity.Success) return identity;

        // the shortcuts are read before anything runs: an app goal that takes a system shortcut's name stops here
        var shortcuts = await shortcut.list.Read();
        if (!shortcuts.Success) return shortcuts;

        // Invariant: every I/O actor must have all three role-channels registered
        // by the entry point before goal execution. Surface a clear error otherwise.
        foreach (var each in actor.list.Items())
        {
            var invariant = each.Channel.Verify();
            if (!invariant.Success) return invariant;
        }

        // Bootstrap runs under System's context; user code runs under User's context (below).
        // Execution flows the actor via its context — there is no global "current actor".
        var context = actor.list.System.Context;

        // Build → PLang builder (runs as User — user is building their code).
        if (Mode.Value == global::app.Mode.Build) return await Build!.Start();

        // Resolve goal file
        var goalFile = await (await context.Variable.Get("goalFile")).Clr<string?>(null);
        if (string.IsNullOrEmpty(goalFile))
            return context.Error(new global::app.error.ServiceError(
                "No goal file specified. Use: plang <goalfile>", "NoGoalFile", 400));

        // The goal file is loaded through the goal collection, which registers what it loads.
        var loaded = await this.goal.Load(goalFile);
        if (!loaded.Success) return loaded;

        var goal = ((await loaded.Value()) as Goal)!;

        // User code executes under the User actor's context — the app starts running through its on.start: a
        // before that fails or cancels is the answer and the goal doesn't start; every after runs on the result.
        var user = actor.list.User.Context;
        var answer = await on.start.Before(this, user);
        var result = answer is { Success: false } or { Handled: true } ? answer : await goal.Start(user);
        return await on.start.After(this, result, user);
    }

    /// <summary>
    /// Starts a goal already in memory, under <paramref name="context"/>.
    /// </summary>
    public async Task<data.@this> Start(Goal goal, actor.context.@this context, CancellationToken ct = default)
    {
        return await goal.Start(context);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Cancel shutdown token
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();

        await actor.list.DisposeAsync();

        await Code.DisposeAsync();
        await KeepAlive.DisposeAsync();
        // The store lets its database go; one that never opened holds nothing.
        store.Dispose();
    }
}
