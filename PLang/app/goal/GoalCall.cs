using app.type.path;
using app.variable;
using app.actor.context;
using app.Attributes;

namespace app.goal;

/// <summary>
/// Strongly-typed reference to a goal, carrying name, parameters, and optional pre-resolved PrPath.
/// PrPath is nullable because dynamic goal names (containing %variable%) can't resolve at build time.
/// </summary>
[PlangType("goal.call")]
public sealed class GoalCall : global::app.type.item.@this, module.IEvent
{
    /// <summary>Event context — set by Events.Stamp when this GoalCall is an event binding.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public module.EventContext? Event { get; set; }

    /// <summary>Goal name to call (e.g., "ProcessData", "Setup/Init").</summary>
    [Store, LlmBuilder, Out]
    public string Name { get; init; } = "";

    /// <summary>Whether this tool is safe for concurrent execution. Default false.</summary>
    [Store, LlmBuilder, Out]
    public bool Parallel { get; init; }

    /// <summary>Parameters to pass to the goal, each as a named Data value.</summary>
    [Store, LlmBuilder, Out]
    public List<data.@this>? Parameters { get; set; }
    /// <summary>Pre-resolved .pr file path. Null when the goal name contains %variables%.</summary>
    [Store, Out]
    public global::app.type.path.@this? PrPath { get; set; }

    /// <summary>The action this GoalCall originated from. Set during parameter resolution.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public steps.step.actions.action.@this? Action { get; set; }

    /// <summary>
    /// OBP: <c>GoalCall</c> owns how a goal-call value is assembled from raw input —
    /// a bare goal name (string), a JSON object (JsonElement), or the dict shape
    /// <c>UnwrapJsonElement</c> produces. A CLR type name leaking into the name slot
    /// is rejected loudly (a build-pipeline ToString() leak, not a goal name).
    /// </summary>
    public static data.@this Convert(object? value, string? kind, actor.context.@this context)
    {
        switch (value)
        {
            case null:
                return data.@this.Ok(value);
            case GoalCall:
                return data.@this.Ok(value);
            case string goalName:
                if (context.App.Type.IsClrTypeName(goalName))
                    return data.@this.FromError(new global::app.error.Error(
                        $"GoalCall.Name was set to a CLR type name '{goalName}' from a string source.",
                        "ClrTypeNameInGoalSlot", 500)
                        { FixSuggestion = "Build pipeline leaked a typed object's ToString() into a goal-name slot." });
                return data.@this.Ok(new GoalCall { Name = goalName });
            case System.Text.Json.JsonElement je:
                try
                {
                    return data.@this.Ok(System.Text.Json.JsonSerializer.Deserialize<GoalCall>(
                        je.GetRawText(), global::app.type.catalog.@this.CaseInsensitiveRead));
                }
                catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
                {
                    return data.@this.FromError(new global::app.error.Error(
                        $"Failed to deserialize GoalCall from JSON: {ex.Message}",
                        "GoalCallDeserializationFailed", 400));
                }
            case IDictionary<string, object?> dict:
                var name = dict.TryGetValue("name", out var n) ? n?.ToString() ?? "" : "";
                // The name slot may be a dynamic %var% (e.g. `call %goalName%`). It stays raw
                // here — GetGoalAsync resolves it at dispatch, in the caller's context. The
                // CLR-type-name guard below still catches a literal leak (a %var% never matches).
                if (context.App.Type.IsClrTypeName(name))
                    return data.@this.FromError(new global::app.error.Error(
                        $"GoalCall.Name was set to a CLR type name '{name}'.",
                        "ClrTypeNameInGoalSlot", 500)
                        { FixSuggestion = "Build pipeline leaked a typed object's ToString() into a goal-name slot " +
                            "(likely a Fluid template rendering an object via ToString() instead of navigating to .Name)." });
                // A null prPath rides as the null.@this singleton after born-native;
                // its ToString is the literal "null", so guard against it — otherwise
                // path.Resolve("null") builds a bogus "/null".
                var prRaw = dict.TryGetValue("prPath", out var pr) && pr is not app.type.@null.@this ? pr : null;
                // Born-native serializes a path as the structured {scheme, relative} object, not a
                // bare string — `prRaw.ToString()` on that yields "Dictionary`2…", which path.Resolve
                // then turns into a bogus "/Dictionary`2…" (the foreach/goal.call "File not found"
                // regression). Take the already-built path, or its "relative" slot, before falling
                // back to a string.
                var prPath = prRaw switch
                {
                    null => null,
                    path builtPath => builtPath,
                    global::app.type.dict.@this nd => ResolveRelative(nd.Get("relative")?.Materialize()?.ToString(), context),
                    IDictionary<string, object?> d2 => ResolveRelative(
                        d2.TryGetValue("relative", out var rel) ? rel?.ToString() : null, context),
                    _ => ResolveRelative(prRaw.ToString(), context),
                };
                List<data.@this>? parameters = null;
                if (dict.TryGetValue("parameters", out var p))
                {
                    // Params ride raw into the call — each is a Data still holding its
                    // %var%/literal/container form. Goal-call shares the caller's scope, so the
                    // step that reads %name% resolves it through the door then; no eager pass.
                    var entries = ParamEntries(p)
                        .Select(e => new data.@this(e.name, e.value) { Context = context })
                        .ToList();
                    if (entries.Count > 0) parameters = entries;
                }
                return data.@this.Ok(new GoalCall { Name = name, PrPath = prPath, Parameters = parameters });
            default:
                return data.@this.FromError(new global::app.error.Error(
                    $"Cannot convert {value.GetType().Name} to a goal call.", "GoalCallConversionFailed", 400));
        }
    }

    // The prPath slot's relative form → a resolved path, or null when absent. (`relative` is
    // the path's own relative member; born-native serializes it inside the {scheme, relative} object.)
    private static path? ResolveRelative(string? relative, actor.context.@this context)
        => string.IsNullOrEmpty(relative) ? null : path.Resolve(relative, context);

    /// <summary>
    /// Normalises the <c>parameters</c> slot to a flat (name, value) sequence,
    /// independent of the container/element CLR shape. The collections-are-data
    /// world hands this slot as a native <c>list.@this</c> of <c>Data</c>-wrapped
    /// <c>dict.@this</c> entries; the legacy/CLR path hands an
    /// <c>IList&lt;object?&gt;</c> of <c>IDictionary&lt;string,object?&gt;</c>. Both
    /// (and a Data-wrapped element of either) collapse here so a goal-call's
    /// <c>goal=%item%</c> parameter is never silently dropped.
    /// </summary>
    private static IEnumerable<(string name, object? value)> ParamEntries(object? p)
    {
        IEnumerable<object?> Elements()
        {
            switch (p)
            {
                case app.type.list.@this nativeList:
                    foreach (var item in nativeList.Items) yield return item;
                    break;
                case System.Collections.IEnumerable seq when p is not string:
                    foreach (var item in seq) yield return item;
                    break;
            }
        }

        foreach (var element in Elements())
        {
            // A native list element is a Data wrapping the entry dict; unwrap it.
            var entry = element is data.@this d ? d.Peek() : element;
            switch (entry)
            {
                case app.type.dict.@this nd:
                    yield return (nd.Get("name")?.Materialize()?.ToString() ?? "", nd.Get("value")?.Materialize());
                    break;
                case IDictionary<string, object?> id:
                    yield return (
                        id.TryGetValue("name", out var en) ? en?.ToString() ?? "" : "",
                        id.TryGetValue("value", out var ev) ? ev : null);
                    break;
            }
        }
    }

    /// <summary>
    /// Resolves the Goal. PrPath is authoritative when set — file.read only.
    /// Otherwise: action chain → app.Goal → file.read fallback.
    /// Returns Data with the Goal as Value, or Data with Error if not found.
    /// </summary>
    public async Task<data.@this> GetGoalAsync(app.@this app, actor.context.@this context)
    {
        // PrPath is authoritative — load from file, no name-based search
        if (PrPath != null)
            return await LoadFromFile(PrPath.ToString(), app, context);

        // A `%var%` goal name (`call %goalName%`) resolves here, at dispatch, in the caller's
        // context — not at conversion. A typed object leaking into the name slot is caught now.
        var resolvedName = Name.Contains('%') ? (await context.Variable.Resolve(Name)) ?? Name : Name;
        if (context.App.Type.IsClrTypeName(resolvedName))
            return data.@this.FromError(new global::app.error.Error(
                $"GoalCall.Name resolved to a CLR type name '{resolvedName}'.", "ClrTypeNameInGoalSlot", 500));

        // 1. Check via the action's step's goal chain (action → step → goal → walk up)
        var currentGoal = Action?.Step?.Goal;
        while (currentGoal != null)
        {
            if (string.Equals(currentGoal.Name, resolvedName, StringComparison.OrdinalIgnoreCase))
                return data.@this.Ok(currentGoal);

            var subGoal = currentGoal.Goals.FirstOrDefault(g =>
                string.Equals(g.Name, resolvedName, StringComparison.OrdinalIgnoreCase));
            if (subGoal != null) return data.@this.Ok(subGoal);

            currentGoal = currentGoal.Parent;
        }

        // 2. Check app's loaded goals
        var loaded = app.Goal.Get(resolvedName);
        if (loaded != null) return data.@this.Ok(loaded);

        // 3. Derive the .pr path from Name and file.read.
        var name = resolvedName.Replace('\\', '/');

        // Caller's folder — the anchor for relative resolution. Compute as a
        // string here because the rest of this method does free-form name math
        // (slash-qualified names, .build prefix, ancestor walks) that's clearer
        // on strings than via Path verbs. Each candidate goes through
        // path.Resolve inside LoadFromFile.
        string? callerDir = Action?.Step?.Goal?.Path?.ToString();
        if (callerDir != null)
        {
            var cut = callerDir.LastIndexOf('/');
            callerDir = cut >= 0 ? callerDir[..cut] : "";
        }

        var slashAt = name.LastIndexOf('/');
        if (slashAt >= 0)
        {
            // Slash-qualified name (BuildGoal/Start): the goal lives in a named
            // folder whose own .build holds the .pr — {folder}/.build/{leaf}.pr,
            // NOT .build/{whole/name}.pr. That folder may be a sibling or an
            // ancestor of the caller's folder, so walk the caller's ancestors
            // before falling back to root- and context-relative.
            var subPath = $"{name[..slashAt]}/.build/{name[(slashAt + 1)..].ToLowerInvariant()}.pr";

            // App-absolute name (e.g. "/system/builder/EmitBuildEvent"): subPath is
            // already app-rooted ("/system/builder/.build/emitbuildevent.pr"). Load it
            // directly — the relative-name fallbacks below ("/" + subPath, {dir}/{subPath})
            // would prepend a second separator, and "//…" resolves to the HOST filesystem
            // root, tripping the permission gate for a file that's plainly under the app.
            if (name.StartsWith('/'))
                return await LoadFromFile(subPath, app, context);

            for (var dir = callerDir; !string.IsNullOrEmpty(dir);)
            {
                var hit = await LoadFromFile($"{dir}/{subPath}", app, context);
                if (hit.Success) return hit;
                var up = dir.LastIndexOf('/');
                dir = up > 0 ? dir[..up] : "";
            }
            var slashRoot = await LoadFromFile("/" + subPath, app, context);
            if (slashRoot.Success) return slashRoot;
            return await LoadFromFile(subPath, app, context);
        }

        // Bare name — the .pr sits in the caller's own .build, else root/context.
        var prFile = $".build/{name.ToLowerInvariant()}.pr";

        // Try relative to the calling goal's folder first (e.g., test sub-goals)
        if (callerDir != null)
        {
            var goalResult = await LoadFromFile($"{callerDir}/{prFile}", app, context);
            if (goalResult.Success) return goalResult;
        }

        // Try root-relative (for user goals calling other goals in same project)
        var rootResult = await LoadFromFile("/" + prFile, app, context);
        if (rootResult.Success) return rootResult;

        // Try context-relative
        return await LoadFromFile(prFile, app, context);
    }

    private async Task<data.@this> LoadFromFile(string prPath, app.@this app, actor.context.@this context)
    {
        var readAction = new module.file.Read
        {
            Context = context,
            Path = data.@this<path>.Ok(path.Resolve(prPath, context))
        };
        var result = await app.RunAction(readAction, context);
        if (!result.Success) return result;

        if (result.Materialize() is not @this goal)
            return data.@this.FromError(new global::app.error.ActionError(
                $"File '{prPath}' did not deserialize to a Goal", "InvalidPrFile", 400));

        // Wire back-references: Goal.App, Step.Goal for root and sub-goals
        goal.App = app;
        foreach (var step in goal.Steps)
            step.Goal = goal;
        foreach (var subGoal in goal.Goals)
        {
            subGoal.App = app;
            subGoal.Parent = goal;
            foreach (var step in subGoal.Steps)
                step.Goal = subGoal;
        }

        // Stash where the .pr was loaded from — Goal.GetRuntimeDirectory uses this
        // so file.read with a relative path resolves against the goal's actual
        // on-disk directory (works in child Apps where Path was set under a
        // different root and would otherwise mis-resolve).
        var prPathResolved = global::app.type.path.@this.Resolve(prPath, context);
        goal.LoadedFromPrPath = prPathResolved;
        foreach (var subGoal in goal.Goals)
            subGoal.LoadedFromPrPath = prPathResolved;

        // Match by name — the loaded goal or one of its sub-goals. A slash-
        // qualified Name (BuildGoal/Start) carries a folder prefix that the
        // loaded goal's own Name never has, so match on the leaf segment.
        var matchName = Name;
        var nameSlash = matchName.LastIndexOfAny(new[] { '/', '\\' });
        if (nameSlash >= 0) matchName = matchName[(nameSlash + 1)..];

        @this? found;
        if (string.IsNullOrEmpty(matchName) || string.Equals(goal.Name, matchName, StringComparison.OrdinalIgnoreCase))
            found = goal;
        else
            found = goal.Goals.FirstOrDefault(g => string.Equals(g.Name, matchName, StringComparison.OrdinalIgnoreCase));

        if (found == null)
            return data.@this.FromError(new global::app.error.ActionError(
                $"Goal '{Name}' not found in '{prPath}'", "GoalNotFound", 404));

        return data.@this.Ok(found);
    }
}
