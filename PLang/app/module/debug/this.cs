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
    public System.Threading.Tasks.ValueTask<setting.@this> Setting() => _context.Setting.Get<setting.@this>();

    // The watched names, as the store names them (no %).
    private async System.Threading.Tasks.ValueTask<HashSet<string>> Watched()
        => (await Setting()).Variables.Items(_context).Select(v => (v.Peek()?.ToString() ?? "").Trim('%'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
        // Debug surface routes via System actor's "error" channel (stderr equivalent), per actor.
        var ch = _context.App.actor.list.System.Channel.Get(app.channel.list.@this.Debug)
              ?? _context.App.actor.list.System.Channel.Get(app.channel.list.@this.Error);
        if (ch == null) return Task.CompletedTask;
        var data = message is app.data.@this d ? d : _context.App.actor.list.System.Context.Ok(message);
        return ch.WriteAsync(data);
    }

    /// <summary>
    /// Activates debug tracing: watches the named variables through the User store's own events,
    /// subscribes the LLM request/response hooks, compiles the grep regex, and registers the
    /// step/goal(/action) event bindings. The config is debug's setting (<c>--debug.setting.…</c> is this run's
    /// values for it, written before Debug is born); this does the side-effects only —
    /// Debug is born, then activated. No config parsing or callstack cross-write lives here
    /// (callstack config is its own flag, <c>--callstack</c>). A grep that isn't a regex is the caller's
    /// error, answered (<c>InvalidPattern</c>) before anything is bound — never a quiet literal match.
    /// </summary>
    public async Task<global::app.error.@this?> Activate()
    {
        var setting = await Setting();
        if (setting.Grep?.ToString() is { Length: > 0 } grep)
        {
            try { _grepRegex = new Regex(grep, RegexOptions.IgnoreCase); }
            catch (ArgumentException ex)
            {
                return new global::app.error.@this($"--debug grep '{grep}' is not a valid regex: {ex.Message}", "InvalidPattern", 400)
                    { Exception = ex };
            }
        }

        // The watch binds after every set and remove of a variable the User actor makes, for the watched names.
        if (setting.Variables.CountRaw > 0)
        {
            var watched = await Watched();
            var variables = _context.App.variable.type.Own();
            bool Watches(global::app.type.item.@this item, actor.context.@this _)
                => item is global::app.type.item.variable.@this variable && watched.Contains(variable.Name);
            variables.Bind("set", When.after, (item, result, context) => Set((global::app.type.item.variable.@this)item, result, context),
                _context.App.actor.list.User, global::app.@event.binding.Scope.actor, Watches);
            variables.Bind("remove", When.after, (item, _, context) => Remove((global::app.type.item.variable.@this)item, context),
                _context.App.actor.list.User, global::app.@event.binding.Scope.actor, Watches);
        }

        // Subscribe to granular LLM tracing — each Llm.* flag writes its own block to the debug channel, whose
        // backing (stderr, a file) decides where it lands.
        if (setting.Llm is { } llm && (llm.System == true || llm.User == true || llm.Response == true || llm.Schema == true))
        {
            if (_context.App.Code.Get<global::app.module.llm.code.ILlm>().Provider is global::app.module.llm.code.OpenAi oai)
            {
                oai.OnBeforeRequest += async (messages, schema) =>
                {
                    if (llm.System)
                    {
                        var sys = messages
                            .Where(m => string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase))
                            .Select(m => m.Content ?? "(null)");
                        await Show("LLM SYSTEM", sys);
                    }
                    if (llm.User)
                    {
                        var users = messages
                            .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
                            .Select(m => m.Content ?? "(null)");
                        await Show("LLM USER", users);
                    }
                    if (llm.Schema == true && !string.IsNullOrEmpty(schema))
                    {
                        await Show("LLM SCHEMA", new[] { schema });
                    }
                };
                if (llm.Response)
                {
                    oai.OnAfterResponse += (rawResponse) =>
                        Show("LLM RESPONSE", new[] { rawResponse ?? "(null)" });
                }
            }
        }

        // Debug watches user execution, so its bindings on the step, goal (and action) types' on.start are the
        // User actor's (where user goals run) — Debug itself is born with System's context. They live as long
        // as the app whose types they are bound on.
        var user = _context.App.actor.list.User;
        var types = _context.App.type.list;
        var step = setting.Step?.ToInt32();
        // debug attaches where the goal named starts (no goal named is every goal), or at its step named, and follows
        // what it calls
        var steps = types["step"].Own();
        var goals = types["goal"].Own();

        // the marks, bound before the watchers so a frame is marked before it is shown
        goals.Bind("start", When.before, (item, _, context) => Attach((global::app.goal.@this)item, context, step), user,
            global::app.@event.binding.Scope.actor, (item, _) => item is global::app.goal.@this);
        if (step != null)
            steps.Bind("start", When.before, (item, _, context) => Attach((global::app.goal.step.@this)item, context, step.Value), user,
                global::app.@event.binding.Scope.actor, (item, _) => item is global::app.goal.step.@this);

        steps.Bind("start", When.before, (item, _, context) => Before((global::app.goal.step.@this)item, context), user,
            global::app.@event.binding.Scope.actor, (item, context) => item is global::app.goal.step.@this && Debugged(context));
        steps.Bind("start", When.after, (item, _, context) => After((global::app.goal.step.@this)item, context), user,
            global::app.@event.binding.Scope.actor, (item, context) => item is global::app.goal.step.@this && Debugged(context));

        goals.Bind("start", When.after, (item, _, context) => After((global::app.goal.@this)item, context), user,
            global::app.@event.binding.Scope.actor, (item, context) => item is global::app.goal.@this && Debugged(context));

        if (setting.Level.Value == global::app.module.debug.Level.Action)
        {
            var actions = types["action"].Own();
            actions.Bind("start", When.before, (item, _, context) => Before((global::app.goal.step.action.@this)item, context), user,
                global::app.@event.binding.Scope.actor, (item, context) => item is global::app.goal.step.action.@this && Debugged(context));
            actions.Bind("start", When.after, (item, _, context) => After((global::app.goal.step.action.@this)item, context), user,
                global::app.@event.binding.Scope.actor, (item, context) => item is global::app.goal.step.action.@this && Debugged(context));
        }
        return null;
    }

    // The frames debug attached at — a named goal's own (or its step named), and each goal one of them calls — held
    // by frame: a frame that ends leaves on its own.
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<global::app.call.@this, @this> _marked = new();

    // A goal starting, in its own frame: marked when it is the goal named (and no step is), or when its caller's run
    // is marked — it is called from inside what debug follows.
    private async Task<data.@this> Attach(global::app.goal.@this goal, actor.context.@this context, int? step)
    {
        if (context.call.Current is { } frame
            && (step == null && await Watches(goal) || frame.Caller?.Within.Any(caller => _marked.TryGetValue(caller, out _)) == true))
            _marked.AddOrUpdate(frame, this);
        return context.Ok();
    }

    // A step starting, in its own frame: marked when it is the step named of the goal named.
    private async Task<data.@this> Attach(global::app.goal.step.@this step, actor.context.@this context, int index)
    {
        if (step.Index == index && await Watches(step.Goal) && context.call.Current is { } frame)
            _marked.AddOrUpdate(frame, this);
        return context.Ok();
    }

    // Whether what runs now is debugged: its goal's run holds a mark.
    private bool Debugged(actor.context.@this context)
        => context.call.Current?.Within.Any(frame => _marked.TryGetValue(frame, out _)) == true;

    // Whether <paramref name="goal"/> is a goal the setting names.
    private async System.Threading.Tasks.ValueTask<bool> Watches(global::app.goal.@this? goal)
        => (await Setting()).Matches(goal?.Name);

    /// <summary>A watched variable set: where it happened and the type it now holds. Answers a plain success — the watch
    /// never fails a program.</summary>
    private async Task<data.@this> Set(global::app.type.item.variable.@this variable, data.@this? stored, actor.context.@this context)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== WATCH [{variable.Name}] SET ===");
        sb.AppendLine($"  {Where(context)}");
        if (stored != null) sb.AppendLine($"  Type: {stored.Type.Name}");
        await Write(sb.ToString());
        return context.Ok();
    }

    /// <summary>A watched variable removed: where it happened.</summary>
    private async Task<data.@this> Remove(global::app.type.item.variable.@this variable, actor.context.@this context)
    {
        await Write($"=== WATCH [{variable.Name}] DELETED ==={Environment.NewLine}  {Where(context)}{Environment.NewLine}");
        return context.Ok();
    }

    // Where a watched change happened: the step running (its label and text, cut to 60), else the goal, else no goal.
    private string Where(actor.context.@this context)
        => context.call.Step is { } step ? $"Step: {step.Label} {(step.Text.Length > 60 ? step.Text[..60] : step.Text)}"
            : context.call.Goal is { } goal ? $"Goal: {goal.Name}"
            : "outside any goal";

    // A step about to run, as debug shows it: its text, its actions as written, the calls it sits in, its variables.
    private async Task<data.@this> Before(global::app.goal.step.@this step, actor.context.@this context)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"=== DEBUG [BEFORE]: Step {step.Label} ===");
        sb.AppendLine($"  Text: {step.Text}");

        foreach (var action in step.Code.Items())   // sync display reads the stored actions, never resolves
        {
            sb.AppendLine($"  Action: {action.Module}.{action.Name}");
            foreach (var p in action.Property)
            {
                // The value as held, never resolved: a BEFORE-step display must not run value doors —
                // resolving renders templates / hops refs (side-effecting, and NREs on a
                // not-yet-ready value), which would perturb the very execution we're observing.
                sb.AppendLine($"    {p.Name} = {await Text(p.Value, context)}");
            }

        }

        var callStack = context.call;
        if (callStack?.Current != null)
        {
            sb.AppendLine("  Call Stack:");
            foreach (var call in callStack.Current.Chain)
                sb.AppendLine($"    at {call}");
        }

        await Variables(sb, step, context);
        sb.AppendLine("========================================");

        await Show(sb);
        return context.Ok();
    }

    // A step that ran, as debug shows it: how long it took, its variables.
    private async Task<data.@this> After(global::app.goal.step.@this step, actor.context.@this context)
    {
        var sb = new StringBuilder();

        // the after-binding runs inside the step's frame: its Duration is the step's time so far
        var took = context.call.Current?.Duration(context) is { } elapsed ? $" ({elapsed.Value.TotalMilliseconds:0.###} ms)" : "";
        sb.AppendLine($"=== DEBUG [AFTER]: Step {step.Label}{took} ===");

        await Variables(sb, step, context);
        sb.AppendLine("========================================");

        await Show(sb);
        return context.Ok();
    }

    /// <summary>
    /// Writes a labeled LLM trace block (e.g. "LLM SYSTEM", "LLM RESPONSE") through
    /// the same filter/truncate pipeline as the rest of debug output. Used by the
    /// granular Llm.* flag handlers — each flag fires its own block independently.
    /// </summary>
    // internal for DebugTraceWriteTests (driving the LLM event lifecycle requires a real LLM call)
    internal Task Show(string title, IEnumerable<string> chunks)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== {title} ===");
        foreach (var chunk in chunks)
            sb.AppendLine(chunk);
        sb.AppendLine($"=== END {title} ===");
        return Show(sb);
    }

    // A block, as debug shows it: the lines its grep keeps, each cut to the length its setting allows, written.
    private async Task Show(StringBuilder sb)
    {
        var maxLen = (await Setting()).Length.Max.ToInt32();
        var grep = _grepRegex;
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

        await Write(output);
    }

    // A goal that ran, as debug shows it.
    private async Task<data.@this> After(global::app.goal.@this goal, actor.context.@this context)
    {
        await Write($"--- DEBUG: Goal '{goal.Name}' completed ---{Environment.NewLine}");
        return context.Ok();
    }

    // An action about to run, as debug shows it: where it is, its step's variables. One composed in C# holds no step.
    private async Task<data.@this> Before(global::app.goal.step.action.@this action, actor.context.@this context)
    {
        if (action.Step is not { } step) return context.Ok();

        var sb = new StringBuilder();
        sb.AppendLine($"  --- ACTION [BEFORE] in Step {step.Label} ---");

        await Variables(sb, step, context);

        await Show(sb);
        return context.Ok();
    }

    // An action that ran, as debug shows it: where it is, its step's variables.
    private async Task<data.@this> After(global::app.goal.step.action.@this action, actor.context.@this context)
    {
        if (action.Step is not { } step) return context.Ok();

        var sb = new StringBuilder();
        sb.AppendLine($"  --- ACTION [AFTER] in Step {step.Label} ---");

        await Variables(sb, step, context);

        await Show(sb);
        return context.Ok();
    }

    // The step's variables, as debug shows them, written into sb.
    private async Task Variables(StringBuilder sb, global::app.goal.step.@this step, actor.context.@this context)
    {
        // the variables the step's values hold, each shown by the name it lives under, then each watched one whole — a
        // path (%!build.setting.cache%) reaches what it names, which its root alone never shows
        var shown = new List<global::app.type.item.variable.@this>();
        var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in step.Code.Items())
            foreach (var p in action.Property)
                if (p.Value is { } value)
                    foreach (var v in value.Variable)
                        if (named.Add(v.Code.Root.Name)) shown.Add(new global::app.type.item.variable.@this(v.Code.Root.Name, context.App!.type.list));
        foreach (var name in await Watched())
            if (new global::app.type.item.variable.parser.@this(name, context.App.type.list).Whole is { } variable && named.Add(variable.Name))
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

            sb.AppendLine($"    %{variable.Name}% = {await Text(data.Peek(), context)} ({data.Type.Name})");

            if (data.Properties.Count > 0)
            {
                sb.AppendLine($"      Properties ({data.Properties.Count}):");
                foreach (var prop in data.Properties)
                {
                    sb.AppendLine($"        {prop.Key} = {await Text(prop.Value, context)}");
                }
            }
        }
    }

    // Debug output can land in logs, terminals, CI artefacts — anywhere — so a value is shown through the one
    // diagnostic door: a structure written in full in the Debug view, a [Sensitive] member masked. Truncation
    // happens at Show via maxLength; a raw collection says how many it holds. A value that cannot be shown
    // shows why — a debug view answers, never ends the program it watches.
    private async System.Threading.Tasks.ValueTask<string> Text(object? value, actor.context.@this context)
    {
        string shown;
        try { shown = await global::app.type.item.@this.Create(value, context).Debug(context); }
        catch (global::app.error.AppException ex) { return $"(cannot be shown: {ex.Error.Message})"; }
        return value switch
        {
            System.Collections.IDictionary d => $"{shown} ({d.Count} keys)",
            System.Collections.IList l => $"{shown} ({l.Count} items)",
            _ => shown,
        };
    }

}
