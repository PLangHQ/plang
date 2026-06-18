using Goal = app.goal.@this;

namespace app.module.builder;

/// <summary>
/// Validates the structural integrity of an LLM build response.
/// Collects ALL errors (step count, indexes, missing actions, unknown modules)
/// and returns them in a structured message so LlmFixer can show them to the LLM.
///
/// Operates on the typed <see cref="BuildResponse"/> so there are no JsonElement /
/// IDictionary forks — the framework's Data&lt;BuildResponse&gt; pipeline deserializes
/// the LLM's JsonElement once with Json.CaseInsensitiveRead.
/// </summary>
[Action("validateResponse")]
public partial class validateResponse : IContext
{
    /// <summary>The LLM response. Framework deserializes the raw JsonElement to BuildResponse.</summary>
    [IsNotNull]
    public partial data.@this<BuildResponse> StepResults { get; init; }

    /// <summary>The goal being built — used to verify step count and prior actions.</summary>
    [IsNotNull]
    public partial data.@this<Goal> Goal { get; init; }

    public async Task<app.data.@this> Run()
    {
        var response = (await StepResults.Value()) as BuildResponse;
        var goal = (await Goal.Value()) as Goal;

        // Identify which parameter is null and dump enough state for LlmFixer +
        // logs to see *why*. "StepResults or Goal is null" was actively misleading —
        // the two failures have completely different causes (LLM response shape vs.
        // %goal% propagation), and a single message hides that.
        if (response == null || goal == null)
        {
            var problems = new List<string>();
            if (response == null)
            {
                var sr = StepResults;
                problems.Add(
                    sr == null
                        ? "StepResults parameter not bound (Data is null)"
                        : sr.IsInitialized
                            ? $"StepResults.Value is null but Data was initialized — deserialization to BuildResponse returned null. Raw value type: {sr.Peek()?.GetType().Name ?? "null"}"
                            : "StepResults parameter is uninitialized (LLM call wrote null/missing %stepResults%)");
            }
            if (goal == null)
            {
                var g = Goal;
                problems.Add(
                    g == null
                        ? "Goal parameter not bound (Data is null)"
                        : g.IsInitialized
                            ? $"Goal.Value is null but Data was initialized. Raw value type: {g.Peek()?.GetType().Name ?? "null"}"
                            : "Goal parameter is uninitialized (%goal% not in scope when builder.validateResponse ran)");
            }
            return app.data.@this.FromError(
                new global::app.error.ActionError(string.Join("; ", problems), "ValidationError", 400));
        }
        return await Validate(response!, goal!, Context.App);
    }

    /// <summary>
    /// Public so SaveGoal can re-run validation as a safety net before persisting.
    /// Builds a fresh BuildResponse from the goal's current Steps and validates.
    /// </summary>
    public static async Task<app.data.@this> ValidateGoalState(Goal goal)
    {
        var response = new BuildResponse
        {
            Steps = goal.Steps.Select(s => new Step
            {
                Index = s.Index,
                Text = s.Text,
                Keep = false,  // post-build state: every step must stand on its own
            }.With(target => CopyActionsIfAny(s, target))).ToList()
        };
        return await Validate(response, goal, goal.App);
    }

    private static Step CopyActionsIfAny(Step from, Step to)
    {
        // CopyActionsIfAny lets the SaveGoal safety net see the same Actions the
        // store would persist — without mutating the source step's ownership.
        foreach (var a in from.Actions) to.Actions.Add(a);
        return to;
    }


    private static async Task<app.data.@this> Validate(BuildResponse response, Goal goal, global::app.@this? app)
    {
        // Auto-fill missing indexes with keep:true placeholders when prior has actions.
        // The LLM sometimes drops a step entirely (omits its index) when it intends
        // "reuse what you had". We synthesize the placeholder so validation passes
        // and enrichResponse copies actions from the prior in-memory step.
        var presentIndexes = new HashSet<int>(response.Steps.Select(s => s.Index));
        for (int i = 0; i < goal.Steps.Count; i++)
        {
            if (presentIndexes.Contains(i)) continue;
            if (goal.Steps[i].Actions.Count == 0) continue;  // nothing to carry forward
            response.Steps.Add(new Step { Index = i, Keep = true });
        }
        response.Steps.Sort((a, b) => a.Index.CompareTo(b.Index));

        var errors = new List<string>();

        if (response.Steps.Count != goal.Steps.Count)
            errors.Add($"Step count: returned {response.Steps.Count}, expected {goal.Steps.Count}. " +
                "Steps are lines starting with '- '. Return exactly one result per goal step, with one or more actions per step.");

        var indexes = new List<int>();
        foreach (var step in response.Steps)
        {
            indexes.Add(step.Index);

            // keep:true short-circuit — prior actions already on the goal step, enrichResponse
            // copies them onto the response step. The only invariant: prior must have actions
            // to keep; otherwise there's nothing to carry forward.
            if (step.Keep)
            {
                if (step.Index >= 0 && step.Index < goal.Steps.Count
                        && goal.Steps[step.Index].Actions.Count == 0)
                    errors.Add($"Step[{step.Index}]: has keep:true but the prior .pr has no actions to keep. Emit a full mapping instead.");
                continue;
            }

            if (step.Actions.Count == 0)
                errors.Add($"Step[{step.Index}]: no actions. Every step must have at least one action with module and action.");
        }

        // Indexes must be 0..N-1 with no gaps.
        if (indexes.Count == response.Steps.Count)
        {
            var sorted = indexes.OrderBy(x => x).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (sorted[i] != i)
                {
                    var expected = string.Join(",", Enumerable.Range(0, response.Steps.Count));
                    var actual = string.Join(",", indexes);
                    errors.Add($"Step indexes must be 0..{response.Steps.Count - 1} with no gaps. Expected [{expected}], got [{actual}].");
                    break;
                }
            }
        }

        // Value-to-type convertibility check. The LLM occasionally hallucinates a
        // value that looks like a schema description (e.g. Actor="this?(user|service|system)")
        // or emits "" as a placeholder for an unset constrained-type parameter.
        // Catching it here surfaces a corrective message LlmFixer can feed back, instead
        // of letting the bad value persist into the .pr and blow up at runtime.
        // Skip: null values, variable references, nested action records (dicts with a
        // "module" key, lists of such), and types we can't resolve. Empty strings on
        // nullable parameters are normalized to null in-place so downstream stages
        // (NormalizeParameterTypes, runtime resolution) treat them like an unset slot.
        var modules = (goal.App ?? app)?.Module;
        foreach (var step in response.Steps)
        {
            if (step.Keep) continue;
            foreach (var a in step.Actions)
            {
                if (a.Parameters == null) continue;

                // Cache the action's schema prop info per (module, action) — looked up
                // once for the parameter loop, used to detect nullable slots.
                var actionType = modules?.GetActionType(a.Module, a.ActionName);

                foreach (var p in a.Parameters)
                {
                    if (p.Type?.Name == null || (await p.Value()) == null) continue;
                    if ((await p.Value()) is global::app.type.text.@this refSv && refSv.StartsWith("%") && refSv.EndsWith("%")) continue;
                    if (ValidateResponseHelpers.IsActionRecord((await p.Value()))) continue;

                    // LLMs emit "" for unset nullable slots even when the prompt says
                    // omit them. Map "" → null when the schema prop is nullable, so the
                    // .pr doesn't store the empty string and TryConvert doesn't get
                    // a string it can't possibly satisfy ("" is not in any actor's
                    // ValidValues, can't parse to int, etc.). For non-nullable slots we
                    // *want* the convertibility error to surface — leave it for the
                    // TryConvert path below.
                    if ((await p.Value()) is global::app.type.text.@this emptyT && !emptyT.IsTruthy())
                    {
                        if (ValidateResponseHelpers.IsNullableSchemaProp(actionType, p.Name))
                        {
                            p.SetValue(null);
                            continue;
                        }
                    }

                    var targetType = (goal.App ?? app)?.Type.Get(p.Type.Name);
                    if (targetType == null) continue;
                    // Scalar PlangTypes (path, tstring, ...) accept the raw primitive at
                    // build time — runtime wraps via Resolve. Already covered by the
                    // shape check above.
                    if (global::app.type.catalog.@this.IsScalarPlangType(targetType)) continue;

                    // [Choices]-bearing types: vocabulary check, not type construction.
                    // Stateful runtime types (Actor) cannot honestly be constructed from
                    // a string at build time — resolution lives on the type. Membership
                    // in the Choices list is the build-time contract; runtime materializes
                    // the chosen name however the type prefers (App.GetActor for Actor,
                    // ctor registry for Operator, ...).
                    var choices = (goal.App ?? app)?.Type.Choices.Get(targetType);
                    if (choices != null)
                    {
                        var sval = (await p.Value()) as global::app.type.text.@this;
                        if (sval != null && choices.Any(c => sval.AreEqual(c)))
                            continue;
                        errors.Add(
                            $"Step[{step.Index}] {a.Module}.{a.ActionName}: parameter '{p.Name}' = {ValidateResponseHelpers.FormatValueForError((await p.Value()))} is not a valid {p.Type.Name}. Valid values: {string.Join(", ", choices)}.");
                        continue;
                    }

                    var (_, error) = global::app.type.catalog.@this.TryConvert((await p.Value()), targetType);
                    if (error == null) continue;

                    var validValues = (goal.App ?? app)?.Type.GetValidValues(targetType);
                    var hint = validValues != null && validValues.Length > 0
                        ? $" Valid values: {string.Join(", ", validValues)}."
                        : "";
                    errors.Add(
                        $"Step[{step.Index}] {a.Module}.{a.ActionName}: parameter '{p.Name}' = {ValidateResponseHelpers.FormatValueForError((await p.Value()))} cannot be converted to type '{p.Type.Name}'.{hint} If the parameter is optional and you don't have a value, omit it from the parameters list — never emit \"\" as a placeholder.");
                }
            }
        }

        if (errors.Count > 0)
        {
            var message = string.Join("\n", errors.Select(e => $"- {e}"));
            return global::app.data.@this.FromError(new global::app.error.ActionError(message, "ValidationErrors", 400));
        }

        return global::app.data.@this.Ok(true);
    }
}

internal static class StepValidationExt
{
    public static T With<T>(this T self, System.Action<T> mutate) { mutate(self); return self; }
}

internal static class ValidateResponseHelpers
{
    /// <summary>
    /// True when the value looks like a nested action record (dict with a "module" key)
    /// or a list of such records — matches the shape the LLM emits for parameters typed
    /// <c>action</c> or <c>list&lt;action&gt;</c>. We don't try to convert these in
    /// the validator; that's enrichResponse's job.
    /// </summary>
    public static bool IsActionRecord(object? v)
    {
        if (v is System.Collections.IDictionary dict)
            return dict.Contains("module") || dict.Contains("Module");
        if (v is System.Collections.IEnumerable list && v is not string)
        {
            foreach (var item in list) return IsActionRecord(item);
        }
        return false;
    }

    public static string FormatValueForError(object? v)
    {
        if (v == null) return "null";
        if (v is string s) return $"\"{s}\"";
        return v.ToString() ?? "(null)";
    }

    /// <summary>
    /// True when the action's parameter slot named <paramref name="paramName"/> is
    /// nullable (Data&lt;T&gt;? or T?). Used to decide whether an LLM-emitted "" can
    /// be safely normalized to null. Null actionType (action not found) returns
    /// false — without schema we can't make the call, fall through to the
    /// convertibility error.
    /// </summary>
    public static bool IsNullableSchemaProp(System.Type? actionType, string paramName)
    {
        if (actionType == null) return false;
        var prop = actionType.GetProperty(paramName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (prop == null) return false;

        // Value type Nullable<T> — Path is Data<T>? where T is class/value-type wrapper.
        if (System.Nullable.GetUnderlyingType(prop.PropertyType) != null) return true;

        // Reference-type nullability (the source-generator emits Data<T>? as an
        // annotated reference type, not Nullable<>). NullabilityInfoContext gives us
        // the Write nullability of the property.
        try
        {
            var nullCtx = new System.Reflection.NullabilityInfoContext();
            return nullCtx.Create(prop).WriteState == System.Reflection.NullabilityState.Nullable;
        }
        catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
        {
            // NullabilityInfoContext can throw on edge-case generic instantiations
            // (e.g. unsupported reflected type). Default to "not nullable" so we err
            // toward surfacing the LLM mistake rather than silently swallowing it.
            return false;
        }
    }
}
