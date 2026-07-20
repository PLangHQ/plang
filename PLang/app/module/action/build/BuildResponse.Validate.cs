using Goal = app.goal.@this;

namespace app.module.action.build;

/// <summary>
/// Validation behavior for <see cref="BuildResponse"/> — the response owns the
/// check of its own structural integrity against the goal it was built for.
/// Collects ALL errors (step count, indexes, missing actions, unconvertible
/// values) so LlmFixer can show them to the LLM in one pass.
/// </summary>
public sealed partial class BuildResponse
{
    /// <summary>
    /// A BuildResponse mirroring a goal's current in-memory Steps — the SaveGoal
    /// safety net validates this before persisting. Every step stands on its own
    /// (keep:false) and carries the same Actions the store would write.
    /// </summary>
    public static BuildResponse FromGoalState(Goal goal) => new()
    {
        Steps = goal.Step.list.Select(s => new Step
        {
            Index = s.Index,
            Text = s.Text,
            Keep = false,  // post-build state: every step must stand on its own
        }.With(target => CopyActionsIfAny(s, target))).ToList()
    };

    private static Step CopyActionsIfAny(Step from, Step to)
    {
        // CopyActionsIfAny lets the SaveGoal safety net see the same Actions the
        // store would persist — without mutating the source step's ownership.
        to.Action = new global::app.goal.step.action.list.@this(from.Action.list);
        return to;
    }

    /// <summary>
    /// Validate this response against <paramref name="goal"/>. Context for the
    /// result Data comes from <c>goal.App</c> (or the explicit <paramref name="app"/>
    /// when the goal isn't yet App-anchored, e.g. mid-build).
    /// </summary>
    public async Task<app.data.@this> Validate(Goal goal, global::app.@this? app = null)
    {
        // Auto-fill missing indexes with keep:true placeholders when prior has actions.
        // The LLM sometimes drops a step entirely (omits its index) when it intends
        // "reuse what you had". We synthesize the placeholder so validation passes
        // and enrichResponse copies actions from the prior in-memory step.
        var presentIndexes = new HashSet<int>(Steps.Select(s => s.Index));
        for (int i = 0; i < goal.Step.Count; i++)
        {
            if (presentIndexes.Contains(i)) continue;
            if (goal.Step[i].Action.Count == 0) continue;  // nothing to carry forward
            Steps.Add(new Step { Index = i, Keep = true });
        }
        Steps.Sort((a, b) => a.Index.CompareTo(b.Index));

        var errors = new List<string>();

        if (Steps.Count != goal.Step.Count)
            errors.Add($"Step count: returned {Steps.Count}, expected {goal.Step.Count}. " +
                "Steps are lines starting with '- '. Return exactly one result per goal step, with one or more actions per step.");

        var indexes = new List<int>();
        foreach (var step in Steps)
        {
            indexes.Add(step.Index);

            // keep:true short-circuit — prior actions already on the goal step, enrichResponse
            // copies them onto the response step. The only invariant: prior must have actions
            // to keep; otherwise there's nothing to carry forward.
            if (step.Keep)
            {
                if (step.Index >= 0 && step.Index < goal.Step.Count
                        && goal.Step[step.Index].Action.Count == 0)
                    errors.Add($"Step[{step.Index}]: has keep:true but the prior .pr has no actions to keep. Emit a full mapping instead.");
                continue;
            }

            if (step.Action.Count == 0)
                errors.Add($"Step[{step.Index}]: no actions. Every step must have at least one action with module and action.");
        }

        // Indexes must be 0..N-1 with no gaps.
        if (indexes.Count == Steps.Count)
        {
            var sorted = indexes.OrderBy(x => x).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (sorted[i] != i)
                {
                    var expected = string.Join(",", Enumerable.Range(0, Steps.Count));
                    var actual = string.Join(",", indexes);
                    errors.Add($"Step indexes must be 0..{Steps.Count - 1} with no gaps. Expected [{expected}], got [{actual}].");
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
        foreach (var step in Steps)
        {
            if (step.Keep) continue;
            foreach (var a in step.Action.list)
            {
                if (a.Parameters == null) continue;

                // The catalog element's declared rows — the ONE reflection site, looked up once for
                // the parameter loop and read for nullable-slot detection (no re-reflection here).
                var rows = modules != null && modules.Contains(a.Module, a.ActionName)
                    ? modules[a.Module][a.ActionName].ParameterRows
                    : null;

                foreach (var p in a.Parameters)
                {
                    // A %ref% parameter resolves at RUNTIME in the caller's scope, not at
                    // build time — skip it WITHOUT opening the door. Resolving %x% here
                    // throws VariableNotFound (this is authored code being validated, not
                    // run). Detect via the non-resolving Peek / the binding's ref flags.
                    if (p.Peek() is global::app.variable.@this || p.HasVariableReference) continue;
                    if (p.Type?.Name == null) continue;

                    var resolved = await p.Value();
                    if (resolved == null) continue;
                    if (ValidateResponseHelpers.IsActionRecord(resolved)) continue;

                    // LLMs emit "" for unset nullable slots even when the prompt says
                    // omit them. Map "" → null when the schema prop is nullable, so the
                    // .pr doesn't store the empty string and TryConvert doesn't get
                    // a string it can't possibly satisfy ("" is not in any actor's
                    // ValidValues, can't parse to int, etc.). For non-nullable slots we
                    // *want* the convertibility error to surface — leave it for the
                    // TryConvert path below.
                    if (resolved is global::app.type.item.text.@this emptyT && !emptyT.IsTruthy())
                    {
                        if (ValidateResponseHelpers.IsNullableSchemaProp(rows, p.Name))
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
                    if (global::app.type.list.@this.IsScalarPlangType(targetType)) continue;

                    // [Choices]-bearing types: vocabulary check, not type construction.
                    // Stateful runtime types (Actor) cannot honestly be constructed from
                    // a string at build time — resolution lives on the type. Membership
                    // in the Choices list is the build-time contract; runtime materializes
                    // the chosen name however the type prefers (App.GetActor for Actor,
                    // ctor registry for Operator, ...).
                    var choices = (goal.App ?? app)?.Type.Choice.Get(targetType);
                    if (choices != null)
                    {
                        var sval = resolved as global::app.type.item.text.@this;
                        if (sval != null && choices.Any(c => sval.AreEqual(c)))
                            continue;
                        errors.Add(
                            $"Step[{step.Index}] {a.Module}.{a.ActionName}: parameter '{p.Name}' = {ValidateResponseHelpers.FormatValueForError(resolved)} is not a valid {p.Type.Name}. Valid values: {string.Join(", ", choices)}.");
                        continue;
                    }

                    // Ask the declared type object whether it can be made from the value.
                    // Create throws on a bad conversion (the throw boundary) — resolved is a
                    // materialized leaf, so this re-types eagerly: a clean conversion means this
                    // parameter is valid; a throw is the bad-literal case that surfaces as a build error.
                    try { p.Type.Create(resolved, (goal.App ?? app)!.User.Context!); continue; }
                    catch (System.Exception ex) when (ex is System.FormatException
                                                      or System.InvalidOperationException or System.Text.Json.JsonException)
                    { /* fall through to record the build error */ }

                    var validValues = (goal.App ?? app)?.Type.GetValidValues(targetType);
                    var hint = validValues != null && validValues.Length > 0
                        ? $" Valid values: {string.Join(", ", validValues)}."
                        : "";
                    errors.Add(
                        $"Step[{step.Index}] {a.Module}.{a.ActionName}: parameter '{p.Name}' = {ValidateResponseHelpers.FormatValueForError(resolved)} cannot be converted to type '{p.Type.Name}'.{hint} If the parameter is optional and you don't have a value, omit it from the parameters list — never emit \"\" as a placeholder.");
                }
            }
        }

        if (errors.Count > 0)
        {
            var message = string.Join("\n", errors.Select(e => $"- {e}"));
            return (goal.App ?? app)!.User.Context!.Error(new global::app.error.ActionError(message, "ValidationErrors", 400));
        }

        return (goal.App ?? app)!.User.Context!.Ok(true);
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
    /// True when the action's parameter slot named <paramref name="paramName"/> is nullable — read
    /// from the catalog element's declared rows (the ONE reflection site owns nullability), not
    /// re-reflected here. Null/absent rows (action not found, or the name isn't a declared slot) →
    /// false: without the schema we can't make the call, fall through to the convertibility error.
    /// </summary>
    public static bool IsNullableSchemaProp(
        System.Collections.Generic.IReadOnlyList<global::app.goal.step.action.property.@this>? rows,
        string paramName)
        => rows?.FirstOrDefault(r => string.Equals(r.Name, paramName, System.StringComparison.OrdinalIgnoreCase))?.Nullable ?? false;
}
