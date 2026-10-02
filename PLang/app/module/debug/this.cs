using System.Text;
using System.Text.RegularExpressions;
using app.actor.context;
using app.@event;

namespace app.module.debug;

/// <summary>
/// Provides debug output for PLang execution when !debug is passed on the command line.
/// Registers events to dump step info, call stack, and memory stack to stderr.
/// </summary>
public sealed class @this
{
    private readonly actor.context.@this _context;

    /// <summary>What debug shows (<c>%!debug%</c>), as its context's settings have it now — a read is a lookup in
    /// their cache, built again only after a write. Activation reads the watched variables, the grep and the LLM
    /// flags once.</summary>
    public setting.@this Setting => _context.Setting.Of<setting.@this>();

    [System.Text.Json.Serialization.JsonIgnore]
    private Regex? _grepRegex;

    public @this(actor.context.@this context) => _context = context;

    /// <summary>
    /// C# diagnostic entrypoint. Writes <paramref name="message"/> to the "debug" channel.
    /// Reached via <c>App.Debug?.Write(...)</c> — the nullable <c>?.</c> IS the gate:
    /// Debug null in production drops the write; non-null under --debug writes it.
    /// Use this instead of Console.WriteLine / System.IO.File.AppendAllText — the channel
    /// is redirectable (stderr by default; users can re-Register "debug" to a file/goal sink).
    /// </summary>
    public Task Write(object? message)
    {
        // Debug surface routes via System actor's "error" channel (stderr equivalent).
        // Stage 6: was app.channels.WriteAsync; now per-actor.
        var ch = _context.App.actor.list.System.Channel.Get(app.channel.list.@this.Debug)
              ?? _context.App.actor.list.System.Channel.Get(app.channel.list.@this.Error);
        if (ch == null) return Task.CompletedTask;
        var envelope = message is app.data.@this d ? d : _context.App.actor.list.System.Context.Ok(message);
        return ch.WriteAsync(envelope);
    }

    // The watched names, as the store names them (no %).
    private HashSet<string> Watched => Setting.Variables.Items(_context).Select(v => (v.Peek()?.ToString() ?? "").Trim('%'))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Activates debug tracing: watches the named variables through the User store's own events,
    /// subscribes the LLM request/response hooks, compiles the grep regex, and registers the
    /// step/goal(/action) event bindings. The config is debug's setting (<c>--debug={…}</c> is this run's
    /// values for it, written before Debug is born); this does the side-effects only —
    /// Debug is born, then activated. No config parsing or callstack cross-write lives here
    /// (callstack config is its own flag, <c>--callstack</c>). A grep that isn't a regex is the caller's
    /// error, answered (<c>InvalidPattern</c>) before anything is bound — never a quiet literal match.
    /// </summary>
    public global::app.error.Error? Activate()
    {
        if (Setting.Grep?.ToString() is { Length: > 0 } grep)
        {
            try { _grepRegex = new Regex(grep, RegexOptions.IgnoreCase); }
            catch (ArgumentException ex)
            {
                return new global::app.error.Error($"--debug grep '{grep}' is not a valid regex: {ex.Message}", "InvalidPattern", 400)
                    { Exception = ex };
            }
        }

        // The watch binds after every set and remove of a variable the User actor makes, for the watched names.
        if (Setting.Variables.CountRaw > 0)
        {
            var watched = Watched;
            var variables = _context.App.variable.Own();
            bool Watches(global::app.type.item.@this item, actor.context.@this _)
                => item is global::app.type.item.variable.@this variable && watched.Contains(variable.Name);
            variables.Bind("set", When.after, (item, result, context) => Watch((global::app.type.item.variable.@this)item, "SET", result, context),
                _context.App.actor.list.User, global::app.@event.binding.Scope.actor, Watches);
            variables.Bind("remove", When.after, (item, result, context) => Watch((global::app.type.item.variable.@this)item, "DELETED", null, context),
                _context.App.actor.list.User, global::app.@event.binding.Scope.actor, Watches);
        }

        // Subscribe to granular LLM tracing — each Llm.* flag writes its own block to the debug channel, whose
        // backing (stderr, a file) decides where it lands.
        if (Setting.Llm is { } llm && (llm.System == true || llm.User == true || llm.Response == true || llm.Schema == true))
        {
            if (_context.App.Code.Get<global::app.module.llm.code.ILlm>().Provider is global::app.module.llm.code.OpenAi oai)
            {
                var context = _context.App.actor.list.User.Context;

                oai.OnBeforeRequest += async (messages, schema) =>
                {
                    if (llm.System)
                    {
                        var sys = messages
                            .Where(m => string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase))
                            .Select(m => m.Content ?? "(null)");
                        await WriteLlmBlock("LLM SYSTEM", sys, context);
                    }
                    if (llm.User)
                    {
                        var users = messages
                            .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
                            .Select(m => m.Content ?? "(null)");
                        await WriteLlmBlock("LLM USER", users, context);
                    }
                    if (llm.Schema == true && !string.IsNullOrEmpty(schema))
                    {
                        await WriteLlmBlock("LLM SCHEMA", new[] { schema }, context);
                    }
                };
                if (llm.Response)
                {
                    oai.OnAfterResponse += (rawResponse) =>
                        WriteLlmBlock("LLM RESPONSE", new[] { rawResponse ?? "(null)" }, context);
                }
            }
        }

        // Debug watches user execution, so its bindings on the step, goal (and action) types' on.start are the
        // User actor's (where user goals run) — Debug itself is born with System's context. They live as long
        // as the app whose types they are bound on.
        var user = _context.App.actor.list.User;
        var types = _context.App.type.list;
        var step = Setting.Step?.ToInt32();

        var steps = types["step"].Own();
        steps.Bind("start", When.before, (_, _, context) => BeforeStepHandler(context, step), user, global::app.@event.binding.Scope.actor,
            (item, _) => item is global::app.goal.step.@this s && Watches(s.Goal));
        steps.Bind("start", When.after, (_, _, context) => AfterStepHandler(context, step), user, global::app.@event.binding.Scope.actor,
            (item, _) => item is global::app.goal.step.@this s && Watches(s.Goal));

        types["goal"].Own().Bind("start", When.after, (_, _, context) => AfterGoalHandler(context), user, global::app.@event.binding.Scope.actor,
            (item, _) => item is global::app.goal.@this g && Watches(g));

        if (Setting.Level.Value == global::app.module.debug.Level.Action)
        {
            var actions = types["action"].Own();
            actions.Bind("start", When.before, (_, _, context) => BeforeActionHandler(context, step), user, global::app.@event.binding.Scope.actor);
            actions.Bind("start", When.after, (_, _, context) => AfterActionHandler(context, step), user, global::app.@event.binding.Scope.actor);
        }
        return null;
    }

    // Whether debug watches <paramref name="goal"/>: every goal (`*`), the goals a `prefix*` starts, or the one
    // named (case aside).
    private bool Watches(global::app.goal.@this? goal)
    {
        var pattern = Setting.Goal?.ToString();
        if (string.IsNullOrEmpty(pattern) || pattern == "*") return true;
        var name = goal?.Name ?? "";
        return pattern.EndsWith('*')
            ? name.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase)
            : string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);
    }


    /// <summary>One watched variable was set or removed: where it happened (goal, step) and, on a set, the
    /// type it now holds. Answers a plain success — the watch never fails a program.</summary>
    private async Task<data.@this> Watch(global::app.type.item.variable.@this variable, string change,
        data.@this? stored, actor.context.@this context)
    {
        var goalName = context.call.Goal?.Name ?? "?";
        var stepIndex = context.call.Step?.Index.ToString() ?? "?";
        var stepText = context.call.Step?.Text;
        if (stepText != null && stepText.Length > 60) stepText = stepText[..60];

        var sb = new StringBuilder();
        sb.AppendLine($"=== WATCH [{variable.Name}] {change} ===");
        sb.AppendLine($"  Goal: {goalName}[{stepIndex}] {stepText ?? "?"}");
        if (stored != null) sb.AppendLine($"  Type: {stored.Type.Name}");
        await Write(sb.ToString());
        return context.Ok();
    }

    private static async Task<data.@this> BeforeStepHandler(actor.context.@this context, int? stepFilter)
    {
        var step = context.call.Step;
        if (step == null) return context.Ok();
        if (stepFilter.HasValue && step.Index != stepFilter.Value) return context.Ok();

        var goalName = step.Goal?.Name ?? "?";
        var sb = new StringBuilder();

        sb.AppendLine($"=== DEBUG [BEFORE]: Step [{step.Index}] of {goalName} ===");
        sb.AppendLine($"  Text: {step.Text}");

        foreach (var action in step.Code.Items())   // sync display reads the stored actions, never resolves
        {
            sb.AppendLine($"  Action: {action.Module}.{action.Name}");
            foreach (var p in action.Property)
            {
                // The value as held, never resolved: a BEFORE-step display must not run value doors —
                // resolving renders templates / hops refs (side-effecting, and NREs on a
                // not-yet-ready value), which would perturb the very execution we're observing.
                sb.AppendLine($"    {p.Name} = {await FormatValue(p.Value, context)}");
            }

        }

        var callStack = context.call;
        if (callStack?.Current != null)
        {
            sb.AppendLine("  Call Stack:");
            foreach (var call in callStack.Current.Chain)
                sb.AppendLine($"    at {call}");
        }

        await AppendStepVariables(sb, context);
        sb.AppendLine("========================================");

        await WriteFiltered(sb, context);
        return context.Ok();
    }

    private static async Task<data.@this> AfterStepHandler(actor.context.@this context, int? stepFilter)
    {
        var step = context.call.Step;
        if (step == null) return context.Ok();
        if (stepFilter.HasValue && step.Index != stepFilter.Value) return context.Ok();

        var goalName = step.Goal?.Name ?? "?";
        var sb = new StringBuilder();

        // the after-binding runs inside the step's frame: its Duration is the step's time so far
        var took = context.call.Current?.Duration is { } elapsed ? $" ({elapsed.TotalMilliseconds:0.###} ms)" : "";
        sb.AppendLine($"=== DEBUG [AFTER]: Step [{step.Index}] of {goalName}{took} ===");

        await AppendStepVariables(sb, context);
        sb.AppendLine("========================================");

        await WriteFiltered(sb, context);
        return context.Ok();
    }

    /// <summary>
    /// Writes a labeled LLM trace block (e.g. "LLM SYSTEM", "LLM RESPONSE") through
    /// the same filter/truncate pipeline as the rest of debug output. Used by the
    /// granular Llm.* flag handlers — each flag fires its own block independently.
    /// </summary>
    // internal for DebugTraceWriteTests (driving the LLM event lifecycle requires a real LLM call)
    internal static Task WriteLlmBlock(string title, IEnumerable<string> chunks, actor.context.@this context)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== {title} ===");
        foreach (var chunk in chunks)
            sb.AppendLine(chunk);
        sb.AppendLine($"=== END {title} ===");
        return WriteFiltered(sb, context);
    }

    private static Task WriteFiltered(StringBuilder sb, actor.context.@this context)
    {
        var debug = context.App?.Debug;
        var maxLen = (debug?.Setting ?? context.Setting.Of<setting.@this>()).Length.Max.ToInt32();
        var grep = debug?._grepRegex;
        var output = sb.ToString();

        // Grep first on full content
        if (grep != null)
        {
            var filtered = new StringBuilder();
            foreach (var line in output.Split('\n'))
            {
                if (grep.IsMatch(line))
                    filtered.AppendLine(line);
            }
            output = filtered.ToString();
        }

        // Then truncate lines for display
        if (maxLen > 0)
        {
            var truncated = new StringBuilder();
            foreach (var line in output.Split('\n'))
            {
                truncated.AppendLine(line.Length > maxLen
                    ? $"{line[..maxLen]}... ({line.Length} chars)"
                    : line);
            }
            output = truncated.ToString();
        }

        return context.App?.Debug?.Write(output) ?? Task.CompletedTask;
    }

    private static async Task<data.@this> AfterGoalHandler(actor.context.@this context)
    {
        var goalName = context.call.Goal?.Name ?? "?";
        var debug = context.App?.Debug;
        if (debug != null)
            await debug.Write($"--- DEBUG: Goal '{goalName}' completed ---{Environment.NewLine}");
        return context.Ok();
    }

    private static async Task<data.@this> BeforeActionHandler(actor.context.@this context, int? stepFilter)
    {
        var step = context.call.Step;
        if (step == null) return context.Ok();
        if (stepFilter.HasValue && step.Index != stepFilter.Value) return context.Ok();

        var goalName = step.Goal?.Name ?? "?";
        var sb = new StringBuilder();
        sb.AppendLine($"  --- ACTION [BEFORE] in Step [{step.Index}] of {goalName} ---");

        await AppendStepVariables(sb, context);

        await WriteFiltered(sb, context);
        return context.Ok();
    }

    private static async Task<data.@this> AfterActionHandler(actor.context.@this context, int? stepFilter)
    {
        var step = context.call.Step;
        if (step == null) return context.Ok();
        if (stepFilter.HasValue && step.Index != stepFilter.Value) return context.Ok();

        var goalName = step.Goal?.Name ?? "?";
        var sb = new StringBuilder();
        sb.AppendLine($"  --- ACTION [AFTER] in Step [{step.Index}] of {goalName} ---");

        await AppendStepVariables(sb, context);

        await WriteFiltered(sb, context);
        return context.Ok();
    }

    private static async Task AppendStepVariables(StringBuilder sb, actor.context.@this context)
    {
        var step = context.call.Step;
        if (step == null) return;

        // the variables the step's values hold, each shown by the name it lives under, then each watched one whole — a
        // path (%!build.setting.cache%) reaches what it names, which its root alone never shows
        var shown = new List<global::app.type.item.variable.@this>();
        var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in step.Code.Items())
            foreach (var p in action.Property)
                if (p.Value is { } value)
                    foreach (var v in value.Variable)
                        if (named.Add(v.Code.Root.Name)) shown.Add(new global::app.type.item.variable.@this(v.Code.Root.Name));
        if (context.App?.Debug is { } debug)
            foreach (var name in debug.Watched)
                if (new global::app.type.item.variable.parser.@this(name).Whole is { } variable && named.Add(variable.Name))
                    shown.Add(variable);

        if (shown.Count == 0) return;

        // each read as the step reads it: an engine path (%!app%) or a shortcut (%!error%, through its goal) is no
        // variable in memory, and reads all the same
        sb.AppendLine($"  Variables ({shown.Count}):");
        foreach (var variable in shown)
        {
            var data = await variable.Start(context);
            if (data is not { IsInitialized: true, Success: true })
            {
                sb.AppendLine($"    %{variable.Name}% = (undefined)");
                continue;
            }

            sb.AppendLine($"    %{variable.Name}% = {await FormatValue(data.Peek(), context)} ({data.Type?.Name ?? "?"})");

            if (data.Properties.Count > 0)
            {
                sb.AppendLine($"      Properties ({data.Properties.Count}):");
                foreach (var prop in data.Properties)
                {
                    sb.AppendLine($"        {prop.Key} = {await FormatValue(prop.Value, context)}");
                }
            }
        }
    }

    // Debug output can land in logs, terminals, CI artefacts — anywhere — so a value is shown through the one
    // diagnostic door: a structure written in full in the Debug view, a [Sensitive] member masked. Truncation
    // happens at WriteFiltered via maxLength; a raw collection says how many it holds.
    private static async System.Threading.Tasks.ValueTask<string> FormatValue(object? value, actor.context.@this context)
    {
        var shown = await global::app.type.item.@this.Create(value, context).Debug(context);
        return value switch
        {
            System.Collections.IDictionary d => $"{shown} ({d.Count} keys)",
            System.Collections.IList l => $"{shown} ({l.Count} items)",
            _ => shown,
        };
    }

}
