using app.variable;

using System.Text;
using System.Text.RegularExpressions;
using app.actor.context;
using app.@event;
using EventBinding = app.@event.lifecycle.binding.@this;

namespace app.module.debug;

/// <summary>
/// Provides debug output for PLang execution when !debug is passed on the command line.
/// Registers events to dump step info, call stack, and memory stack to stderr.
/// </summary>
public sealed class @this
{
    private readonly app.@this _engine;

    /// <summary>
    /// Whether debug mode is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Filter to a specific goal name. Null = all goals.
    /// </summary>
    public string? Goal { get; set; }

    /// <summary>
    /// Filter to a specific step index. Null = all steps.
    /// </summary>
    public int? Step { get; set; }

    /// <summary>
    /// Variables to watch. Each can optionally track events (OnCreate/OnChange/OnDelete).
    /// Variables without Event set are displayed at step boundaries.
    /// Set via: --debug={"variables":[{"name":"trace","event":"onchange"}]}
    /// </summary>
    public List<DebugVariable>? Variables { get; set; }

    /// <summary>
    /// Max characters per line before truncation. Default 500.
    /// </summary>
    public int MaxLength { get; set; } = 500;

    /// <summary>
    /// Regex string to filter debug output lines.
    /// </summary>
    public string? Grep { get; set; }

    /// <summary>
    /// Debug detail level. "step" (default) or "action" (shows state between actions).
    /// </summary>
    public string Level { get; set; } = "step";

    /// <summary>
    /// When true, errors include a dump of all available variables at the point of failure.
    /// Useful for diagnosing missing variables in foreach/goal.call chains.
    /// Set via: --debug={"verbose":true}
    /// </summary>
    public bool Verbose { get; set; }

    /// <summary>
    /// Granular LLM tracing — each sub-flag dumps one part of the API exchange to stderr.
    /// Set via: --debug={"llm":{"system":true,"user":true,"response":true,"schema":true}}
    /// Only the parts you set to true are shown — all-off (or no Llm object) means no tracing.
    /// </summary>
    public LlmDebug? Llm { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    private Regex? _grepRegex;
    private bool _applied;

    /// <summary>
    /// Path of the file the *current* LLM call's blocks land in. Set by
    /// OnBeforeRequest, read by OnAfterResponse so request + response share one file.
    /// LLM calls are sync so a single field suffices — no queue needed.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    internal global::app.type.path.@this? _currentLlmFilePath;

    /// <summary>
    /// Per-process counter for disambiguating LLM call retries. Increments on every
    /// OnBeforeRequest. LlmFixer reuses the same (goal, step, trace.id) — without
    /// this counter the retry would overwrite the original file we want to inspect.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    private int _llmCallCounter;

    public @this(app.@this engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// C# diagnostic entrypoint. Writes <paramref name="message"/> to the "debug" channel
    /// when <see cref="IsEnabled"/> is true, otherwise no-op (zero-cost for production).
    /// Use this in runtime code instead of Console.WriteLine / System.IO.File.AppendAllText
    /// — the channel is redirectable (stderr by default; users can re-Register "debug" to
    /// route to a file or a goal-backed sink).
    /// </summary>
    public Task Write(object? message)
    {
        if (!IsEnabled) return Task.CompletedTask;
        // Debug surface routes via System actor's "error" channel (stderr equivalent).
        // Stage 6: was app.channels.WriteAsync; now per-actor.
        var ch = _engine.System.Channel.Resolve(app.channel.list.@this.Debug)
              ?? _engine.System.Channel.Resolve(app.channel.list.@this.Error);
        if (ch == null) return Task.CompletedTask;
        var envelope = message is app.data.@this d ? d : app.data.@this.Ok(message);
        return ch.WriteAsync(envelope);
    }

    public void Apply(object debugValue)
    {
        // Idempotent: subscribing twice would double every event handler and
        // duplicate every diagnostic line. One Apply per Debug instance.
        if (_applied) return;
        _applied = true;
        IsEnabled = true;

        if (debugValue is IDictionary<string, object?> dict)
        {
            // String shorthand for variables: ["foo","bar"] → [{name:"foo"},{name:"bar"}].
            // Populate's generic list converter can't bind a bare string to DebugVariable.
            if (dict.TryGetValue("variables", out var rawVars) && rawVars is System.Collections.IList list)
            {
                var normalized = new List<object?>(list.Count);
                foreach (var item in list)
                {
                    if (item is string s)
                        normalized.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["name"] = s });
                    else
                        normalized.Add(item);
                }
                dict["variables"] = normalized;
            }

            // CallStack flags: {callstack:true} → Shorthand (Timing+Tags on); {callstack:{...}}
            // → field-by-field. Populate would set a field on `this` — we want to set the
            // flags on the already-constructed CallStack, so handle this key explicitly and
            // strip it before the generic Populate.
            if (dict.TryGetValue("callstack", out var rawCallstack))
            {
                _engine.CallStack.Flags = app.callstack.Flags.Parse(rawCallstack);
                dict.Remove("callstack");
            }

            global::app.type.catalog.@this.Populate(this, dict);
        }

        // Strip % from variable names
        if (Variables != null)
        {
            foreach (var v in Variables)
                v.Name = v.Name.Trim('%');

            // Create placeholder Data with event handlers for watched variables
            var vars = _engine.User.Context.Variable;
            foreach (var v in Variables.Where(v => v.Event.HasValue))
            {
                var placeholder = data.@this.Uninitialized(v.Name);
                if (v.Event == DebugEvent.OnCreate)
                    placeholder.OnCreate.Add((data) => LogEvent(v.Name, "CREATED", data));
                if (v.Event == DebugEvent.OnChange)
                    placeholder.OnChange.Add((oldData, newData) => LogMutation(v.Name, oldData, newData));
                if (v.Event == DebugEvent.OnDelete)
                    placeholder.OnDelete.Add((data) => LogEvent(v.Name, "DELETED", data));
                if (v.Event == DebugEvent.OnTypeChange)
                    placeholder.OnChange.Add((oldData, newData) =>
                    {
                        var oldType = oldData.RawValue?.GetType().Name;
                        var newType = newData.RawValue?.GetType().Name;
                        if (oldType != newType) LogMutation(v.Name, oldData, newData);
                    });
                vars.Set(placeholder);
            }
        }

        // Subscribe to granular LLM tracing — each Llm.* flag emits its own block to stderr or file.
        if (Llm != null && (Llm.System || Llm.User || Llm.Response || Llm.Schema))
        {
            var provider = _engine.Code.Get<global::app.module.llm.code.ILlm>();
            if (provider.Success && provider.Value is global::app.module.llm.code.OpenAi oai)
            {
                var context = _engine.User.Context;
                var toFile = string.Equals(Llm.Output, "file", StringComparison.OrdinalIgnoreCase);

                oai.OnBeforeRequest += (messages, schema) =>
                {
                    // Resolve file path *once* per call so request + response share it.
                    if (toFile) _currentLlmFilePath = ResolveLlmFilePath(context);

                    if (Llm.System)
                    {
                        var sys = messages
                            .Where(m => string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase))
                            .Select(m => m.Content ?? "(null)");
                        EmitLlmBlock("LLM SYSTEM", sys, context, toFile);
                    }
                    if (Llm.User)
                    {
                        var users = messages
                            .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
                            .Select(m => m.Content ?? "(null)");
                        EmitLlmBlock("LLM USER", users, context, toFile);
                    }
                    if (Llm.Schema && !string.IsNullOrEmpty(schema))
                    {
                        EmitLlmBlock("LLM SCHEMA", new[] { schema }, context, toFile);
                    }
                };
                if (Llm.Response)
                {
                    oai.OnAfterResponse += (rawResponse) =>
                        EmitLlmBlock("LLM RESPONSE", new[] { rawResponse ?? "(null)" }, context, toFile);
                }
            }
        }

        // Build grep regex
        if (!string.IsNullOrEmpty(Grep))
        {
            try { _grepRegex = new Regex(Grep, RegexOptions.IgnoreCase); }
            catch (ArgumentException) { _grepRegex = new Regex(Regex.Escape(Grep), RegexOptions.IgnoreCase); }
        }

        var events = _engine.CurrentActor.Context.Events;

        events.Register(new EventBinding(
            Trigger.BeforeStep,
            (context, _, _) => BeforeStepHandler(context, Step),
            goalNamePattern: Goal ?? "*",
            priority: int.MaxValue,
            stopOnError: false));

        events.Register(new EventBinding(
            Trigger.AfterStep,
            (context, _, _) => AfterStepHandler(context, Step),
            goalNamePattern: Goal ?? "*",
            priority: int.MaxValue,
            stopOnError: false));

        events.Register(new EventBinding(
            Trigger.AfterGoal,
            (context, _, _) => AfterGoalHandler(context),
            goalNamePattern: Goal ?? "*",
            priority: int.MaxValue,
            stopOnError: false));

        if (string.Equals(Level, "action", StringComparison.OrdinalIgnoreCase))
        {
            events.Register(new EventBinding(
                Trigger.BeforeAction,
                (context, _, _) => BeforeActionHandler(context, Step),
                goalNamePattern: Goal ?? "*",
                priority: int.MaxValue,
                stopOnError: false));

            events.Register(new EventBinding(
                Trigger.AfterAction,
                (context, _, _) => AfterActionHandler(context, Step),
                goalNamePattern: Goal ?? "*",
                priority: int.MaxValue,
                stopOnError: false));
        }
    }


    public void LogMutation(string name, data.@this oldData, data.@this newData)
    {
        var context = _engine.User.Context;
        var goalName = context?.Goal?.Name ?? "?";
        var stepIndex = context?.Step?.Index.ToString() ?? "?";
        var stepText = context?.Step?.Text;
        if (stepText != null && stepText.Length > 60) stepText = stepText[..60];
        var stack = new System.Diagnostics.StackTrace(2, true);

        var sb = new StringBuilder();
        sb.AppendLine($"=== WATCH [{name}] CHANGED ===");
        sb.AppendLine($"  Goal: {goalName}[{stepIndex}] {stepText ?? "?"}");
        sb.AppendLine($"  Raw: {oldData.RawValue?.GetType().Name ?? "null"} → {newData.RawValue?.GetType().Name ?? "null"}");
        sb.AppendLine($"  Value: {oldData.Value?.GetType().Name ?? "null"} → {newData.Value?.GetType().Name ?? "null"}");
        sb.AppendLine($"  HasCtx: {newData.Context != null}");
        for (int i = 0; i < Math.Min(5, stack.FrameCount); i++)
        {
            var frame = stack.GetFrame(i);
            if (frame?.GetMethod() != null)
                sb.AppendLine($"  at {frame.GetMethod()!.DeclaringType?.Name}.{frame.GetMethod()!.Name}:{frame.GetFileLineNumber()}");
        }
        sb.AppendLine("==============================");
        // Subscribed as sync OnChange callback — fire-and-forget through the
        // debug channel. Console.Error was non-awaitable too, so ordering
        // guarantees are unchanged.
        _ = Write(sb.ToString());
    }

    public void LogEvent(string name, string eventType, data.@this data)
    {
        var context = _engine.User.Context;
        var goalName = context?.Goal?.Name ?? "?";
        var stepIndex = context?.Step?.Index.ToString() ?? "?";

        _ = Write($"=== WATCH [{name}] {eventType} in {goalName}[{stepIndex}] type={data.Value?.GetType().Name ?? "null"} ==={Environment.NewLine}");
    }

    private static async Task<data.@this> BeforeStepHandler(actor.context.@this context, int? stepFilter)
    {
        var step = context.Step;
        if (step == null) return app.data.@this.Ok();
        if (stepFilter.HasValue && step.Index != stepFilter.Value) return app.data.@this.Ok();

        var goalName = context.Goal?.Name ?? "?";
        var sb = new StringBuilder();

        sb.AppendLine($"=== DEBUG [BEFORE]: Step [{step.Index}] of {goalName} ===");
        sb.AppendLine($"  Text: {step.Text}");

        foreach (var action in step.Actions)
        {
            sb.AppendLine($"  Action: {action.Module}.{action.ActionName}");
            foreach (var p in action.Parameters)
            {
                sb.AppendLine($"    {p.Name} = {FormatValue(p.Value, context)}");
            }

        }

        var callStack = context.CallStack;
        if (callStack?.Current != null)
        {
            sb.AppendLine("  Call Stack:");
            foreach (var call in callStack.Current.SnapshotChain())
            {
                var goal = call.Action.Step?.Goal;
                var stepIdx = call.Action.Step?.Index ?? -1;
                var name = goal?.Name ?? call.Action.Module;
                var stepInfo = stepIdx >= 0 ? $" (step {stepIdx + 1})" : "";
                var pathInfo = goal?.Path != null ? $" in {goal.Path}" : "";
                sb.AppendLine($"    at {name}.{call.Action.ActionName}{stepInfo}{pathInfo}");
            }
        }

        AppendStepVariables(sb, context);
        sb.AppendLine("========================================");

        await WriteFiltered(sb, context);
        return app.data.@this.Ok();
    }

    private static async Task<data.@this> AfterStepHandler(actor.context.@this context, int? stepFilter)
    {
        var step = context.Step;
        if (step == null) return app.data.@this.Ok();
        if (stepFilter.HasValue && step.Index != stepFilter.Value) return app.data.@this.Ok();

        var goalName = context.Goal?.Name ?? "?";
        var sb = new StringBuilder();

        sb.AppendLine($"=== DEBUG [AFTER]: Step [{step.Index}] of {goalName} ===");

        AppendStepVariables(sb, context);
        sb.AppendLine("========================================");

        await WriteFiltered(sb, context);
        return app.data.@this.Ok();
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
    internal void EmitLlmBlock(string title, IEnumerable<string> chunks, actor.context.@this context, bool toFile)
    {
        if (toFile && _currentLlmFilePath != null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== {title} ===");
            foreach (var chunk in chunks)
                sb.AppendLine(chunk);
            sb.AppendLine($"=== END {title} ===");
            try
            {
                // Append routes through AuthGate(Write). Sync-wait — trace
                // emission is inside sync event handlers.
                var written = _currentLlmFilePath.Append(sb.ToString()).GetAwaiter().GetResult();
                if (!written.Success)
                    _ = Write($"[debug] LLM file write failed: {written.Error?.Message} (path={_currentLlmFilePath}){Environment.NewLine}");
            }
            catch (System.Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
            {
                _ = Write($"[debug] LLM file write failed: {ex.Message} (path={_currentLlmFilePath}){Environment.NewLine}");
            }
            return;
        }

        _ = WriteLlmBlock(title, chunks, context);
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
    internal global::app.type.path.@this ResolveLlmFilePath(actor.context.@this context)
    {
        _llmCallCounter++;

        var traceId = context.Trace.Id;

        var goalData = context.Variable.Get("goal");
        var goalName = "unknown";
        if (goalData != null && goalData.Value != null)
        {
            var nameProp = goalData.Value.GetType().GetProperty("Name");
            if (nameProp != null)
                goalName = nameProp.GetValue(goalData.Value)?.ToString() ?? "unknown";
        }

        var stepData = context.Variable.Get("step");
        var stepKey = "goal";
        if (stepData != null && stepData.IsInitialized && stepData.Value != null)
        {
            var idxProp = stepData.Value.GetType().GetProperty("Index");
            if (idxProp != null)
            {
                var idx = idxProp.GetValue(stepData.Value);
                if (idx != null) stepKey = idx.ToString() ?? "goal";
            }
        }

        var safeGoal = SanitizeFilenamePart(goalName);
        // Derive directory via path verbs. Mkdir routes through AuthGate(Write);
        // .build/ is in-root so it fast-passes.
        var traceDir = global::app.type.path.@this.Resolve("/.build/traces", context)
            .Combine(traceId).Combine("llm");
        traceDir.Mkdir().GetAwaiter().GetResult();

        // First call to a given (goal, step) gets a clean name; subsequent retries get _N.
        var basePath = traceDir.Combine($"{safeGoal}_{stepKey}.txt");
        if (basePath.ExistsAsync().GetAwaiter().GetResult() is { Success: true, Value.Value: false }) return basePath;

        for (int n = 2; n < 100; n++)
        {
            var candidate = traceDir.Combine($"{safeGoal}_{stepKey}_{n}.txt");
            if (candidate.ExistsAsync().GetAwaiter().GetResult() is { Success: true, Value.Value: false }) return candidate;
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
        var maxLen = debug?.MaxLength ?? 500;
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
        var goalName = context.Goal?.Name ?? "?";
        var debug = context.App?.Debug;
        if (debug != null)
            await debug.Write($"--- DEBUG: Goal '{goalName}' completed ---{Environment.NewLine}");
        return app.data.@this.Ok();
    }

    private static async Task<data.@this> BeforeActionHandler(actor.context.@this context, int? stepFilter)
    {
        var step = context.Step;
        if (step == null) return app.data.@this.Ok();
        if (stepFilter.HasValue && step.Index != stepFilter.Value) return app.data.@this.Ok();

        var goalName = context.Goal?.Name ?? "?";
        var sb = new StringBuilder();
        sb.AppendLine($"  --- ACTION [BEFORE] in Step [{step.Index}] of {goalName} ---");

        AppendStepVariables(sb, context);

        await WriteFiltered(sb, context);
        return app.data.@this.Ok();
    }

    private static async Task<data.@this> AfterActionHandler(actor.context.@this context, int? stepFilter)
    {
        var step = context.Step;
        if (step == null) return app.data.@this.Ok();
        if (stepFilter.HasValue && step.Index != stepFilter.Value) return app.data.@this.Ok();

        var goalName = context.Goal?.Name ?? "?";
        var sb = new StringBuilder();
        sb.AppendLine($"  --- ACTION [AFTER] in Step [{step.Index}] of {goalName} ---");

        AppendStepVariables(sb, context);

        await WriteFiltered(sb, context);
        return app.data.@this.Ok();
    }

    private static readonly Regex VarRefPattern = new(@"%([^%]+)%", RegexOptions.Compiled);

    private static void AppendStepVariables(StringBuilder sb, actor.context.@this context)
    {
        var step = context.Step;
        if (step == null) return;

        var varNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var action in step.Actions)
        {
            foreach (var p in action.Parameters)
            {
                if (p.Value is string s)
                {
                    foreach (Match m in VarRefPattern.Matches(s))
                        varNames.Add(m.Groups[1].Value);
                }
            }
        }

        // Add explicitly watched variables
        var watchVars = context.App?.Debug.Variables;
        if (watchVars != null)
            foreach (var v in watchVars)
                varNames.Add(v.Name);

        if (varNames.Count == 0) return;

        sb.AppendLine($"  Variables ({varNames.Count}):");
        foreach (var name in varNames)
        {
            var data = context.Variable.Get(name);
            if (data == null || !data.IsInitialized)
            {
                sb.AppendLine($"    %{name}% = (undefined)");
                continue;
            }

            sb.AppendLine($"    %{name}% = {FormatValue(data.Value, context)} ({data.Type?.Name ?? "?"})");

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
            Modifiers = { global::app.channel.serializer.filter.Sensitive.Strip }
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
                .Where(p => p.CanRead && p.Name != "EqualityContract")
                .Take(5)
                .Select(p =>
                {
                    try { return $"{p.Name}={TruncateToString(p.GetValue(value), 40)}"; }
                    catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException)) { return $"{p.Name}=?"; }
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

public enum DebugEvent { OnCreate, OnChange, OnDelete, OnTypeChange }

public class DebugVariable
{
    public string Name { get; set; } = "";
    public DebugEvent? Event { get; set; }
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
    public bool System { get; set; }

    /// <summary>Dump user (and any non-system) messages from each LLM API call.</summary>
    public bool User { get; set; }

    /// <summary>Dump the raw response string returned by the LLM API.</summary>
    public bool Response { get; set; }

    /// <summary>Dump the JSON Schema string passed via the format instruction.</summary>
    public bool Schema { get; set; }

    /// <summary>
    /// Where enabled blocks go. "stderr" (default) = existing labeled blocks to stderr,
    /// subject to maxLength truncation. "file" = full untruncated blocks to a per-call
    /// file at .build/traces/llm/{goalName}_{stepKey}_{traceId}.txt and stderr is suppressed.
    /// File mode is the only way to get the full system prompt or raw response when they
    /// exceed maxLength, since maxLength is for terminal display.
    /// </summary>
    public string Output { get; set; } = "stderr";
}
