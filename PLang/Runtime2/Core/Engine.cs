using PLang.Runtime2.Context;
using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;
using PLang.Runtime2.modules;
using PLang.Runtime2.Serialization;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PLang.Runtime2.Core;

/// <summary>
/// Main runtime engine for PLang Runtime2.
/// Executes goals and manages the execution lifecycle.
/// Self-contained: owns all app-level state (environment, culture, shutdown, key-value store).
/// </summary>
public sealed class Engine : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, object> _data = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Libraries _libraries;
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
    /// Relative root path, always "/".
    /// </summary>
    public string Path => "/";

    /// <summary>
    /// The OS absolute path of the application (e.g. C:\myapp or /home/user/app).
    /// </summary>
    public string AbsolutePath { get; }

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
    public Events Events { get; }

    /// <summary>
    /// The library registry — uniform handler resolution system.
    /// Built-in handlers are Libraries[0], external DLLs can be added as additional libraries.
    /// </summary>
    public Libraries Libraries => _libraries;

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
    public Runtime2.IO.Channels Channels { get; }

    /// <summary>
    /// Pluggable step cache. Default: in-memory. Swap via: - use 'redis.dll' for caching
    /// </summary>
    public ICache Cache { get; set; } = new MemoryStepCache();

    /// <summary>
    /// Whether debug mode is enabled.
    /// </summary>
    public bool IsDebugMode { get; set; }

    /// <summary>
    /// Whether test mode is enabled.
    /// </summary>
    public bool IsTestMode { get; set; }

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

    #region Key-Value Store

    /// <summary>
    /// Gets or sets a value in the engine's key-value store.
    /// </summary>
    public object? this[string key]
    {
        get => _data.TryGetValue(key, out var value) ? value : null;
        set
        {
            if (value == null)
                _data.TryRemove(key, out _);
            else
                _data[key] = value;
        }
    }

    /// <summary>
    /// Gets a typed value from the engine's key-value store.
    /// </summary>
    public T? Get<T>(string key)
    {
        if (_data.TryGetValue(key, out var value) && value is T typed)
            return typed;
        return default;
    }

    /// <summary>
    /// Sets a typed value in the engine's key-value store.
    /// </summary>
    public void Set<T>(string key, T value)
    {
        if (value == null)
            _data.TryRemove(key, out _);
        else
            _data[key] = value;
    }

    /// <summary>
    /// Gets a value or creates it if it doesn't exist.
    /// </summary>
    public T GetOrCreate<T>(string key, Func<T> factory) where T : class
    {
        return (T)_data.GetOrAdd(key, _ => factory()!);
    }

    /// <summary>
    /// Checks if a key exists.
    /// </summary>
    public bool ContainsKey(string key) => _data.ContainsKey(key);

    /// <summary>
    /// Removes a key.
    /// </summary>
    public bool Remove(string key) => _data.TryRemove(key, out _);

    /// <summary>
    /// Gets all keys.
    /// </summary>
    public IEnumerable<string> Keys => _data.Keys;

    #endregion

    /// <summary>
    /// Requests graceful shutdown.
    /// </summary>
    public void RequestShutdown()
    {
        _shutdownCts.Cancel();
    }

    public Engine(Interfaces.IPLangFileSystem fileSystem)
        : this(fileSystem.RootDirectory, fileSystem: fileSystem)
    {
    }

    public Engine(string absolutePath, Libraries? libraries = null,
        SerializerRegistry? serializers = null, Interfaces.IPLangFileSystem? fileSystem = null,
        string? environment = null)
    {
        Id = Guid.NewGuid().ToString("N")[..12];
        Name = "Runtime2";
        AbsolutePath = absolutePath;
        Environment = environment ?? "production";
        StartedAt = DateTime.UtcNow;
        Events = new Events();
        _libraries = libraries ?? new Libraries();
        _serializers = serializers ?? new SerializerRegistry();
        _goals = new Goals { Engine = this };
        FileSystem = fileSystem ?? CreateDefaultFileSystem(absolutePath);
        Channels = new Runtime2.IO.Channels(this);
    }

    /// <summary>
    /// Runs a goal via a strongly-typed GoalCall. Resolves %var% in Name, tries PrPath first, falls back to name lookup.
    /// </summary>
    public async Task<Data> RunGoalAsync(GoalCall goalCall, PLangContext? context = null, CancellationToken cancellationToken = default)
    {
        context ??= User.Context;

        // Resolve %var% references in the goal name
        var resolvedName = ResolveVariables(goalCall.Name, context.MemoryStack);

        // Inject GoalCall parameters into the context's MemoryStack
        if (goalCall.Parameters != null)
        {
            foreach (var param in goalCall.Parameters)
                context.MemoryStack.Set(param.Key, param.Value);
        }

        // Try PrPath first (when available)
        if (!string.IsNullOrEmpty(goalCall.PrPath))
        {
            var goal = await _goals.GetByPrPathAsync(goalCall.PrPath, cancellationToken);
            if (goal != null)
                return await RunGoalAsync(goal, context, cancellationToken);
        }

        // Fall back to name-based lookup
        return await _goals.Run(resolvedName, context, cancellationToken);
    }

    /// <summary>
    /// Resolves %variable% patterns in a string using the memory stack.
    /// </summary>
    private static string ResolveVariables(string input, MemoryStack memoryStack)
    {
        if (string.IsNullOrEmpty(input) || !input.Contains('%'))
            return input;

        return Regex.Replace(input, @"%([^%]+)%", match =>
        {
            var varName = match.Groups[1].Value;
            var value = memoryStack.GetValue(varName);
            return value?.ToString() ?? match.Value;
        });
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
        var context = new PLangContext(this, memoryStack)
        {
            CallStack = new CallStack()
        };
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
    /// Resolves a value from the engine's key-value store.
    /// If the value is a GoalCall, executes the goal and returns the result.
    /// </summary>
    public async Task<object?> ResolveAsync(string key, PLangContext? context = null)
    {
        var value = this[key];
        if (value is GoalCall goalCall)
        {
            context ??= User.Context;
            var result = await RunGoalAsync(goalCall, context);
            return result.Success ? result.Value : null;
        }
        return value;
    }

    /// <summary>
    /// Resolves a typed value from the engine's key-value store.
    /// If the value is a GoalCall, executes the goal and returns the typed result.
    /// </summary>
    public async Task<T?> ResolveAsync<T>(string key, PLangContext? context = null)
    {
        var value = await ResolveAsync(key, context);
        if (value is T typed) return typed;
        return default;
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
        foreach (var handler in _libraries.All)
        {
            if (handler is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else if (handler is IDisposable disposable)
                disposable.Dispose();
        }

        // Dispose any disposable items in the key-value store
        foreach (var value in _data.Values)
        {
            if (value is IDisposable disposable)
                disposable.Dispose();
        }
        _data.Clear();
    }
}
