using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using app.Attributes;
using Force.DeepCloner;

namespace app.module.builder;

/// <summary>
/// Builder mode controller. When enabled, actors use in-memory datasources
/// so the builder can validate SQL against real schema without creating files.
/// Activated by: plang p build
/// </summary>
public sealed partial class @this
{
    private readonly app.@this _app;

    /// <summary>
    /// Whether build mode is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Optional file filter. When set, only these files are built — IN ORDER.
    /// Set via --build={"files":"test.goal"} or --build={"files":["test.goal","run.goal"]}
    /// </summary>
    public List<path> Files { get; set; } = new();

    /// <summary>
    /// Whether to use LLM cache. Default true. Set via --build={"cache":false}
    /// </summary>
    public bool Cache { get; set; } = true;

    /// <summary>
    /// Snapshot of .pr file content (raw JSON) loaded at first access during build.
    /// Keyed by absolute file path. When a .pr file is overwritten during build,
    /// the snapshot provides the original content for re-deserialization.
    /// </summary>
    private readonly Dictionary<string, string> _prSnapshot = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// JsonSerializerOptions for .pr file writes — only properties marked with [Store] are
    /// included. CamelCase, indented, nulls omitted. Stage 27 absorbed from Utils.Json.PrWrite.
    /// Internal so the test fixture can reach it.
    /// </summary>
    internal static readonly JsonSerializerOptions PrWrite = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new global::app.channel.serializer.json.Converter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { StoreOnlyModifier }
        }
    };

    private static void StoreOnlyModifier(JsonTypeInfo typeInfo)
    {
        // Only filter properties on our own types (Goal, Step, Action, etc.)
        // Leave framework types (List, Dictionary, Data, etc.) alone.
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

        var ns = typeInfo.Type.Namespace;
        if (ns == null || !ns.StartsWith("app.goal", StringComparison.Ordinal)) return;

        foreach (var prop in typeInfo.Properties)
        {
            if (prop.AttributeProvider == null) continue;

            var hasStore = prop.AttributeProvider
                .GetCustomAttributes(typeof(StoreAttribute), inherit: true)
                .Length > 0;

            if (!hasStore)
                prop.ShouldSerialize = (_, _) => false;
        }
    }

    /// <summary>
    /// Snapshots .pr file content if not already captured.
    /// Called from file.Read paths during building.
    /// </summary>
    public void SnapshotPrFile(string absolutePath, string content)
    {
        _prSnapshot.TryAdd(absolutePath, content);
    }

    /// <summary>
    /// Gets snapshotted .pr file content. Returns null if not snapshotted.
    /// </summary>
    public string? GetPrSnapshot(string absolutePath)
    {
        return _prSnapshot.TryGetValue(absolutePath, out var content) ? content : null;
    }

    public @this(app.@this app)
    {
        _app = app;
    }

    /// <summary>The context this system-owned collection births its result Data from.</summary>
    private actor.context.@this Context => _app.System.Context;

    /// <summary>
    /// Build-mode bootstrap. Confirms the app should be created (interactive y/n
    /// prompt) when no <c>.build/app.pr</c> exists and <c>--app={"create":true}</c>
    /// wasn't passed; switches to the User actor; dispatches the system Build goal.
    /// Headless / CI-redirected stdin returns NoAppFound rather than blocking on a
    /// prompt nobody can answer.
    /// </summary>
    public async Task<data.@this> RunAsync()
    {
        var appPrPath = global::app.type.path.@this.Resolve("/.build/app.pr", _app.System.Context!);
        var appPrExists = await appPrPath.ExistsAsync();
        // No app marker on disk → confirm creation (or error when headless).
        // Was inverted (fired when the marker DID exist) — that forced every
        // build of an existing app to need --app={"create":true}.
        if ((!appPrExists.Success || (await appPrExists.Value())?.Value != true) && !_app.Create)
        {
            if (Console.IsInputRedirected)
                return Context.Error(new global::app.error.ServiceError(
                    $"No app found at {_app.AbsolutePath}. Run plang build from your app's root directory, or use --app={{\"create\":true}}.", "NoAppFound", 400));

            // Channels are wired by the entry point (PlangConsole) before Run.
            // The User actor's "output"/"input" channels wrap stdout/stdin — write
            // the prompt to output, then ReadLine off the input stream. Two-call
            // because the default channels are direction-split (output write-only,
            // input read-only) so Stream.Ask can't bridge them.
            var outputChannel = _app.User.Channel.Get(global::app.channel.list.@this.Output) as global::app.channel.type.stream.@this;
            var inputChannel = _app.User.Channel.Get(global::app.channel.list.@this.Input) as global::app.channel.type.stream.@this;
            if (outputChannel == null || inputChannel == null)
                return Context.Error(new global::app.error.ServiceError(
                    "Default channels not wired — cannot prompt for app creation.", "MissingRequiredChannelAtBoot", 500));

            await outputChannel.WriteTextAsync($"No app found at {_app.AbsolutePath}. Create new app? (y/n): ");
            using var reader = new StreamReader(inputChannel.Stream, leaveOpen: true);
            var answer = (await reader.ReadLineAsync())?.Trim().ToLowerInvariant();
            if (answer != "y" && answer != "yes")
                return Context.Error(new global::app.error.ServiceError(
                    "Build cancelled. Run plang build from your app's root directory.", "BuildCancelled", 400));
        }

        _app.CurrentActor = _app.User;
        var buildCall = new GoalCall { Name = "Build", PrPath = global::app.type.path.@this.Resolve("/system/builder/.build/build.pr", _app.User.Context) };
        return await _app.RunGoalAsync(buildCall, _app.User.Context);
    }
}
