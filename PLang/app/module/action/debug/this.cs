using System.Text;
using System.Text.RegularExpressions;
using app.actor.context;
using app.@event;

namespace app.module.action.debug;

/// <summary>
/// Provides debug output for PLang execution when !debug is passed on the command line.
/// Registers events to dump step info, call stack, and memory stack to stderr.
/// </summary>
public sealed class @this
{
    private readonly actor.context.@this _context;

    /// <summary>What debug shows (<c>%!debug%</c>) — held, since every step reads it; the app builds it again
    /// when a value under its path is written. Activation reads the watched variables, the grep and the LLM
    /// flags once.</summary>
    public setting.@this Setting { get; internal set; }

    [System.Text.Json.Serialization.JsonIgnore]
    private Regex? _grepRegex;

    /// <summary>
    /// Path of the file the *current* LLM call's blocks land in. Set by
    /// OnBeforeRequest, read by OnAfterResponse so request + response share one file.
    /// LLM calls are sync so a single field suffices — no queue needed.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    internal global::app.type.item.path.@this? _currentLlmFilePath;

    /// <summary>
    /// Per-process counter for disambiguating LLM call retries. Increments on every
    /// OnBeforeRequest. LlmFixer reuses the same (goal, step, trace.id) — without
    /// this counter the retry would overwrite the original file we want to inspect.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    private int _llmCallCounter;

    public @this(actor.context.@this context)
    {
        _context = context;
        Setting = context.Setting.Of<setting.@this>();
    }

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
    /// (callstack config is its own flag, <c>--callstack</c>).
    /// </summary>
    public void Activate()
    {
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

        // Subscribe to granular LLM tracing — each Llm.* flag emits its own block to stderr or file.
        if (Setting.Llm is { } llm && (llm.System == true || llm.User == true || llm.Response == true || llm.Schema == true))
        {
            if (_context.App.Code.Get<global::app.module.action.llm.code.ILlm>().Provider is global::app.module.action.llm.code.OpenAi oai)
            {
                var context = _context.App.actor.list.User.Context;
                var toFile = (TraceOutput)llm.Output == TraceOutput.file;

                oai.OnBeforeRequest += async (messages, schema) =>
                {
                    // Resolve file path *once* per call so request + response share it.
                    if (toFile) _currentLlmFilePath = ResolveLlmFilePath(context);

                    if (llm.System)
                    {
                        var sys = messages
                            .Where(m => string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase))
                            .Select(m => m.Content ?? "(null)");
                        await EmitLlmBlock("LLM SYSTEM", sys, context, toFile);
                    }
                    if (llm.User)
                    {
                        var users = messages
                            .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
                            .Select(m => m.Content ?? "(null)");
                        await EmitLlmBlock("LLM USER", users, context, toFile);
                    }
                    if (llm.Schema == true && !string.IsNullOrEmpty(schema))
                    {
                        await EmitLlmBlock("LLM SCHEMA", new[] { schema }, context, toFile);
                    }
                };
                if (llm.Response)
                {
                    oai.OnAfterResponse += (rawResponse) =>
                        EmitLlmBlock("LLM RESPONSE", new[] { rawResponse ?? "(null)" }, context, toFile);
                }
            }
        }

        // Build grep regex
        if (Setting.Grep?.ToString() is { Length: > 0 } grep)
        {
            // a --grep that isn't a regex is the caller's error, named — never a quiet literal match
            try { _grepRegex = new Regex(grep, RegexOptions.IgnoreCase); }
            catch (ArgumentException ex)
            {
                throw new global::app.error.AppException($"--debug grep '{grep}' is not a valid regex: {ex.Message}", ex, "InvalidPattern", 400);
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

        if (Setting.Level.Value == global::app.module.action.debug.Level.Action)
        {
            var actions = types["action"].Own();
            actions.Bind("start", When.before, (_, _, context) => BeforeActionHandler(context, step), user, global::app.@event.binding.Scope.actor);
            actions.Bind("start", When.after, (_, _, context) => AfterActionHandler(context, step), user, global::app.@event.binding.Scope.actor);
        }
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
        var goalName = context.CallStack.Goal?.Name ?? "?";
        var stepIndex = context.CallStack.Step?.Index.ToString() ?? "?";
        var stepText = context.CallStack.Step?.Text;
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
        var step = context.CallStack.Step;
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
                sb.AppendLine($"    {p.Name} = {FormatValue(p.Value, context)}");
            }

        }

        var callStack = context.CallStack;
        if (callStack?.Current != null)
        {
            sb.AppendLine("  Call Stack:");
            foreach (var call in callStack.Current.SnapshotChain())
                sb.AppendLine($"    at {call}");
        }

        AppendStepVariables(sb, context);
        sb.AppendLine("========================================");

        await WriteFiltered(sb, context);
        return context.Ok();
    }

    private static async Task<data.@this> AfterStepHandler(actor.context.@this context, int? stepFilter)
    {
        var step = context.CallStack.Step;
        if (step == null) return context.Ok();
        if (stepFilter.HasValue && step.Index != stepFilter.Value) return context.Ok();

        var goalName = step.Goal?.Name ?? "?";
        var sb = new StringBuilder();

        // the after-binding runs inside the step's frame: its Duration is the step's time so far
        var took = context.CallStack.Current?.Duration is { } elapsed ? $" ({elapsed.TotalMilliseconds:0.###} ms)" : "";
        sb.AppendLine($"=== DEBUG [AFTER]: Step [{step.Index}] of {goalName}{took} ===");

        AppendStepVariables(sb, context);
        sb.AppendLine("========================================");

        await WriteFiltered(sb, context);
        return context.Ok();
    }

    /// <summary>
    /// Writes a labeled LLM trace block (e.g. "LLM SYSTEM", "LLM RESPONSE") through
    /// the same filter/truncate pipeline as the rest of debug output. Used by the
    /// granular Llm.* flag handlers — each flag fires its own block independently.
    /// </summary>
    private static Task WriteLlmBlock(string title, IEnumerable<string> chunks, actor.context.@this context)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== {title} ===");
        foreach (var chunk in chunks)
            sb.AppendLine(chunk);
        sb.AppendLine($"=== END {title} ===");
        return WriteFiltered(sb, context);
    }

    /// <summary>
    /// Routes an LLM block to either stderr (default) or the per-call file at
    /// <c>.build/traces/{trace.id}/llm/{goalName}_{stepKey}.txt</c>. File mode skips
    /// the maxLength truncation and stderr — the whole point of file mode is to
    /// capture the full untruncated content for callers that exceed the terminal limit.
    /// </summary>
    // internal for DebugTraceWriteTests to drive the trace-write path
    // (driving the full event lifecycle requires a real LLM call).
    internal async Task EmitLlmBlock(string title, IEnumerable<string> chunks, actor.context.@this context, bool toFile)
    {
        if (toFile && _currentLlmFilePath != null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== {title} ===");
            foreach (var chunk in chunks)
                sb.AppendLine(chunk);
            sb.AppendLine($"=== END {title} ===");
            // Append routes through AuthGate(Write); a refused or failed write is reported on the debug channel.
            var written = await _currentLlmFilePath.Append(sb.ToString(), context);
            if (!written.Success)
                await Write($"[debug] LLM file write failed: {written.Error?.Message} (path={_currentLlmFilePath}){Environment.NewLine}");
            return;
        }

        await WriteLlmBlock(title, chunks, context);
    }

    /// <summary>
    /// Builds the file path for the current LLM call. Reads:
    /// - <c>trace.id</c> from <see cref="actor.context.@this.Trace"/> (C#-owned, born with Context).
    /// - <c>%goal%</c> PLang variable (the user goal being built — set by the builder).
    ///   Different from <c>context.Goal</c>, which is the *runtime* goal currently executing
    ///   (typically the builder's own goal, e.g. BuildGoal — not what we want to label by).
    /// - <c>%step%</c> PLang variable when present (BuildStep sets it for per-step LLM calls);
    ///   absent for goal-level calls (BuildGoalCore), which use the literal "goal" as stepKey.
    /// - <c>_llmCallCounter</c> appended when the same (goal, step) fires more than once
    ///   in this process (LlmFixer retries reuse the same key).
    /// </summary>
    internal global::app.type.item.path.@this ResolveLlmFilePath(actor.context.@this context)
    {
        _llmCallCounter++;

        var traceId = context.Trace.Id;

        var goalData = context.Variable.Peek("goal");
        var goalName = "unknown";
        if (goalData != null && goalData.Peek() != null)
        {
            var nameProp = goalData.Peek()!.GetType().GetProperty("Name");
            if (nameProp != null)
                goalName = nameProp.GetValue(goalData.Peek())?.ToString() ?? "unknown";
        }

        var stepData = context.Variable.Peek("step");
        var stepKey = "goal";
        if (stepData != null && stepData.IsInitialized && stepData.Peek() != null)
        {
            var idxProp = stepData.Peek()!.GetType().GetProperty("Index");
            if (idxProp != null)
            {
                var idx = idxProp.GetValue(stepData.Peek());
                if (idx != null) stepKey = idx.ToString() ?? "goal";
            }
        }

        var safeGoal = SanitizeFilenamePart(goalName);
        // Derive directory via path verbs. Mkdir routes through AuthGate(Write);
        // .build/ is in-root so it fast-passes.
        var traceDir = global::app.type.item.path.@this.Resolve("/.build/traces", context)
            .Combine(traceId).Combine("llm");
        traceDir.Mkdir(context).GetAwaiter().GetResult();

        // First call to a given (goal, step) gets a clean name; subsequent retries get _N.
        var basePath = traceDir.Combine($"{safeGoal}_{stepKey}.txt");
        { var __e = basePath.ExistsAsync(context).GetAwaiter().GetResult(); if (__e.Success && (__e.Peek() as global::app.type.item.@bool.@this)?.Value == false) return basePath; }

        for (int n = 2; n < 100; n++)
        {
            var candidate = traceDir.Combine($"{safeGoal}_{stepKey}_{n}.txt");
            { var __e = candidate.ExistsAsync(context).GetAwaiter().GetResult(); if (__e.Success && (__e.Peek() as global::app.type.item.@bool.@this)?.Value == false) return candidate; }
        }
        // Fallback if 100 retries somehow aren't enough — counter guarantees uniqueness.
        return traceDir.Combine($"{safeGoal}_{stepKey}_call{_llmCallCounter}.txt");
    }

    // Conservative invalid-filename character set covering both Unix and
    // Windows — keeps the sanitizer free of System.IO.Path reaches per the
    // PLNG002 ban.
    private static readonly char[] _invalidFileNameChars =
        ['<', '>', ':', '"', '/', '\\', '|', '?', '*', '\0'];

    private string SanitizeFilenamePart(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append(Array.IndexOf(_invalidFileNameChars, c) >= 0 || char.IsControl(c) ? '_' : c);
        return sb.ToString();
    }

    private static Task WriteFiltered(StringBuilder sb, actor.context.@this context)
    {
        var debug = context.App?.Debug;
        var maxLen = (debug?.Setting ?? context.Setting.Of<setting.@this>()).MaxLength.ToInt32();
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
        var goalName = context.CallStack.Goal?.Name ?? "?";
        var debug = context.App?.Debug;
        if (debug != null)
            await debug.Write($"--- DEBUG: Goal '{goalName}' completed ---{Environment.NewLine}");
        return context.Ok();
    }

    private static async Task<data.@this> BeforeActionHandler(actor.context.@this context, int? stepFilter)
    {
        var step = context.CallStack.Step;
        if (step == null) return context.Ok();
        if (stepFilter.HasValue && step.Index != stepFilter.Value) return context.Ok();

        var goalName = step.Goal?.Name ?? "?";
        var sb = new StringBuilder();
        sb.AppendLine($"  --- ACTION [BEFORE] in Step [{step.Index}] of {goalName} ---");

        AppendStepVariables(sb, context);

        await WriteFiltered(sb, context);
        return context.Ok();
    }

    private static async Task<data.@this> AfterActionHandler(actor.context.@this context, int? stepFilter)
    {
        var step = context.CallStack.Step;
        if (step == null) return context.Ok();
        if (stepFilter.HasValue && step.Index != stepFilter.Value) return context.Ok();

        var goalName = step.Goal?.Name ?? "?";
        var sb = new StringBuilder();
        sb.AppendLine($"  --- ACTION [AFTER] in Step [{step.Index}] of {goalName} ---");

        AppendStepVariables(sb, context);

        await WriteFiltered(sb, context);
        return context.Ok();
    }

    private static void AppendStepVariables(StringBuilder sb, actor.context.@this context)
    {
        var step = context.CallStack.Step;
        if (step == null) return;

        var varNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // the variables the step's values hold, each shown by the name it lives under
        foreach (var action in step.Code.Items())
            foreach (var p in action.Property)
                if (p.Value is { } value)
                    foreach (var v in value.Variable)
                        varNames.Add(v.Code.Root.Name);

        // Add explicitly watched variables
        if (context.App?.Debug is { } debug)
            foreach (var name in debug.Watched)
                varNames.Add(new global::app.type.item.variable.parser.@this(name).Whole?.Code.Root.Name ?? name);

        if (varNames.Count == 0) return;

        sb.AppendLine($"  Variables ({varNames.Count}):");
        foreach (var name in varNames)
        {
            var data = context.Variable.Peek(name);
            if (data == null || !data.IsInitialized)
            {
                sb.AppendLine($"    %{name}% = (undefined)");
                continue;
            }

            sb.AppendLine($"    %{name}% = {FormatValue(data.Peek(), context)} ({data.Type?.Name ?? "?"})");

            if (data.Properties.Count > 0)
            {
                sb.AppendLine($"      Properties ({data.Properties.Count}):");
                foreach (var prop in data.Properties)
                {
                    sb.AppendLine($"        {prop.Key} = {FormatValue(prop.Value, context)}");
                }
            }
        }
    }

    private static string FormatValue(object? value, actor.context.@this context)
    {
        // Always format full content — truncation happens at WriteFiltered via maxLength.
        // Dictionaries/lists serialize to JSON so diagnostic output carries full structure;
        // the older 3-key/1-item preview threw away exactly the content we want to see when
        // chasing null-valued-variable bugs. Preview remains as a fallback on serialization
        // failure (e.g. cyclic graphs, non-serializable types).
        if (value == null) return "(null)";
        if (value is string s) return $"\"{s}\"";
        if (value is System.Collections.IDictionary or System.Collections.IList)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(value, _debugJsonOptions);
                var count = value is System.Collections.IDictionary d ? d.Count
                          : value is System.Collections.IList l ? l.Count : 0;
                var suffix = value is System.Collections.IDictionary ? $" ({count} keys)"
                           : $" ({count} items)";
                return json + suffix;
            }
            catch (System.Exception ex) when (ex is System.Text.Json.JsonException || ex is NotSupportedException) { /* fall through to preview */ }
        }
        if (value is System.Collections.IEnumerable enumerable and not string)
        {
            int count = 0;
            object? first = null;
            foreach (var item in enumerable) { if (count == 0) first = item; count++; }
            if (count == 0) return "[0 items]";
            var firstStr = FormatPreviewValue(first);
            return count == 1 ? $"[1 item: {firstStr}]" : $"[{count} items, first: {firstStr}]";
        }
        var str = value.ToString() ?? "(null)";
        return str;
    }

    // Debug output can land in logs, terminals, CI artefacts — anywhere. Strip [Sensitive]
    // properties so api keys, passwords, private settings never leak through diagnostic
    // paths. Uses the same SensitivePropertyFilter that the channel serializers use, so
    // the sensitive-stripping rule has a single source of truth.
    private static readonly System.Text.Json.JsonSerializerOptions _debugJsonOptions = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver
        {
            Modifiers = { global::app.type.format.filter.Sensitive.Strip }
        }
    };

    private static string FormatPreviewValue(object? value)
    {
        if (value == null) return "(null)";
        if (value is string s) return s.Length > 80 ? $"\"{s[..80]}...\" ({s.Length}c)" : $"\"{s}\"";
        if (value is System.Collections.IDictionary dict)
        {
            var parts = new List<string>();
            var i = 0;
            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                if (i++ >= 4) { parts.Add("..."); break; }
                parts.Add($"{entry.Key}={TruncateToString(entry.Value, 40)}");
            }
            return $"{{ {string.Join(", ", parts)} }}";
        }
        if (value is System.Collections.ICollection col)
            return $"[{col.Count} items]";

        // For objects: show public property names and short values
        var type = value.GetType();
        if (!type.IsPrimitive && type != typeof(decimal) && type != typeof(DateTime)
            && type != typeof(Guid) && !type.IsEnum)
        {
            var props = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(p => p.CanRead && p.Name != "EqualityContract" && p.GetIndexParameters().Length == 0)
                .Take(5)
                .Select(p =>
                {
                    try { return $"{p.Name}={TruncateToString(p.GetValue(value), 40)}"; }
                    // a getter that throws shows as `?` in the preview; a fault outside the getter bubbles
                    catch (System.Reflection.TargetInvocationException) { return $"{p.Name}=?"; }
                });
            var propStr = string.Join(", ", props);
            if (!string.IsNullOrEmpty(propStr))
                return $"{{ {propStr} }}";
        }

        return TruncateToString(value, 80);
    }

    private static string TruncateToString(object? value, int max)
    {
        if (value == null) return "null";
        if (value is string s) return s.Length > max ? $"\"{s[..max]}...[{s.Length - max} more chars]\"" : $"\"{s}\"";
        var str = value.ToString() ?? "?";
        return str.Length > max ? $"{str[..max]}...[{str.Length - max} more chars]" : str;
    }
}

/// <summary>
/// Granular LLM trace flags. Each flag dumps one slice of the API exchange — set
/// only what you want to see. Flags compose: enabling System and Response gives
/// the system prompt plus the raw model response, with no user-message or schema noise.
/// Set via: --debug={"llm":{"system":true,"user":true,"response":true,"schema":true}}
/// </summary>
public class LlmDebug
{
    /// <summary>Dump system messages from each LLM API call.</summary>
    public global::app.type.item.@bool.@this System { get; set; } = false;

    /// <summary>Dump user (and any non-system) messages from each LLM API call.</summary>
    public global::app.type.item.@bool.@this User { get; set; } = false;

    /// <summary>Dump the raw response string returned by the LLM API.</summary>
    public global::app.type.item.@bool.@this Response { get; set; } = false;

    /// <summary>Dump the JSON Schema string passed via the format instruction.</summary>
    public global::app.type.item.@bool.@this Schema { get; set; } = false;

    /// <summary>
    /// Where enabled blocks go. "stderr" (default) = existing labeled blocks to stderr,
    /// subject to maxLength truncation. "file" = full untruncated blocks to a per-call
    /// file at .build/traces/llm/{goalName}_{stepKey}_{traceId}.txt and stderr is suppressed.
    /// File mode is the only way to get the full system prompt or raw response when they
    /// exceed maxLength, since maxLength is for terminal display.
    /// </summary>
    public global::app.type.item.choice.@this<TraceOutput> Output { get; set; } = TraceOutput.stderr;
}

/// <summary>Where an enabled LLM trace block goes: labeled to stderr (truncated to maxLength), or whole to a
/// per-call file.</summary>
[global::app.Attributes.PlangType("traceoutput")]
public enum TraceOutput
{
    stderr,
    file,
}
