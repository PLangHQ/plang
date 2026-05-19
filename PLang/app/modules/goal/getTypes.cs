using System.Reflection;
using app.actor.context;

namespace app.modules.goal;

/// <summary>
/// Walks a goal's steps forward and infers, per step, which variables are in scope
/// and what type each carries. Used at build time so the LLM prompt for step N can
/// show "%foo%(string), %goals%(list&lt;goal&gt;)" — type information the model
/// would otherwise have to guess at from step text alone.
///
/// Walk is forward-only — the snapshot recorded against step N only contains variables
/// written by steps 0..N-1. A reassignment overwrites the prior type in the working map.
/// Variables introduced by a `loop.foreach`'s ItemName land in the snapshot for the
/// SAME step that introduces them — the step's body needs to reference them.
///
/// Type sources, in priority order:
///   1. `variable.set Name=%x%, Value=%__data__%` → previous producing action's return type
///      (reflected from its handler's Run() method).
///   2. `variable.set Name=%x%, Value=&lt;literal&gt;` → the Value parameter's `type` field on the .pr.
///   3. `loop.foreach Collection=%xs%, ItemName=%y%` → element type of %xs%
///      (strip outer "list&lt;…&gt;" from the known type of %xs%, falling back to "object").
///
/// Returned shape: `list&lt;dict&lt;string,string&gt;&gt;` — outer index is the step index
/// (positional, same as `goal.Steps`), inner key is the variable name (without leading
/// %, lowercased), value is the PLang type name (string, int, list&lt;goal&gt;, path, etc.).
/// List-shape instead of dict&lt;int, ...&gt; so PLang's `%xs[stepResult.index]%`
/// indexing syntax (which works for List&lt;T&gt; the same way `goal.Steps[N]` does) resolves
/// cleanly without needing a key-type coercion.
/// </summary>
[ModuleDescription("Goal introspection — extract per-step variable types for build-time prompt enrichment")]
[System.ComponentModel.Description("Walk the goal's steps and return a per-step map of variable names to their inferred PLang types")]
[Action("getTypes")]
public partial class getTypes : IContext
{
    public partial data.@this<global::app.goals.goal.@this> Goal { get; init; }

    public Task<data.@this> Run()
    {
        var goal = Goal.Value!;
        var modules = Context.App!.Modules;

        var perStep = new List<Dictionary<string, string>>(goal.Steps.Count);
        var working = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Tracks the return type of the most recent producing action in the current step's
        // action chain — needed so `variable.set Value=%__data__%` can take the prior
        // action's type. Reset at the start of every step (no cross-step %__data__%).
        string? chainReturnType = null;

        for (int i = 0; i < goal.Steps.Count; i++)
        {
            var snapshot = new Dictionary<string, string>(working, StringComparer.OrdinalIgnoreCase);
            perStep.Add(snapshot);
            chainReturnType = null;

            foreach (var action in goal.Steps[i].Actions ?? new())
            {
                ProcessAction(action, working, snapshot, ref chainReturnType, modules);
            }
        }

        // List indexed by step position — `%variablesByStep[stepResult.index]%` works
        // out of the box without dict-key coercion.
        return Task.FromResult(data.@this.Ok(perStep));
    }

    private static void ProcessAction(
        global::app.goals.goal.steps.step.actions.action.@this action,
        Dictionary<string, string> working,
        Dictionary<string, string> currentStepSnapshot,
        ref string? chainReturnType,
        modules.@this modules)
    {
        if (string.Equals(action.Module, "variable", StringComparison.OrdinalIgnoreCase)
         && string.Equals(action.ActionName, "set", StringComparison.OrdinalIgnoreCase))
        {
            var nameParam = ParamByName(action, "Name");
            var valueParam = ParamByName(action, "Value");
            var typeParam = ParamByName(action, "Type");
            if (nameParam?.Value is string rawName && !string.IsNullOrEmpty(rawName))
            {
                string type;
                if (valueParam?.Value is string sval && string.Equals(sval, "%__data__%", StringComparison.OrdinalIgnoreCase))
                {
                    type = chainReturnType ?? "object";
                }
                else if (typeParam?.Value is string explicitType && !string.IsNullOrEmpty(explicitType))
                {
                    // The optional Type=json override (set %x% = {...}, type=json).
                    type = explicitType;
                }
                else
                {
                    type = (valueParam?.Type?.Value as string) ?? "object";
                }
                working[Normalise(rawName)] = type;
            }
            chainReturnType = null;  // variable.set consumed %__data__%
            return;
        }

        if (string.Equals(action.Module, "loop", StringComparison.OrdinalIgnoreCase)
         && string.Equals(action.ActionName, "foreach", StringComparison.OrdinalIgnoreCase))
        {
            var collectionParam = ParamByName(action, "Collection");
            var itemNameParam = ParamByName(action, "ItemName");
            if (itemNameParam?.Value is string rawItemName && !string.IsNullOrEmpty(rawItemName))
            {
                var collectionType = "object";
                if (collectionParam?.Value is string collRef && collRef.StartsWith("%"))
                {
                    if (working.TryGetValue(Normalise(collRef), out var collKnown))
                        collectionType = collKnown;
                }
                var elementType = ElementOf(collectionType);
                var itemKey = Normalise(rawItemName);
                working[itemKey] = elementType;
                // Also write into the current step's snapshot — ItemName is bound for the
                // foreach step's own body, so the LLM rendering this step needs to see the
                // item variable's type when sanity-checking `item=%var%` against goal.call.
                currentStepSnapshot[itemKey] = elementType;
            }
            chainReturnType = null;
            return;
        }

        // Generic action — record its return type so a following variable.set picking up
        // %__data__% sees it. Reflection on the handler's Run() method handles the
        // common case (`Task<Data<T>>` → T). For actions returning bare Task<Data> the
        // chain type stays "object" — that's the lower bound, not a regression.
        chainReturnType = DetermineReturnType(action, modules);
    }

    private static global::app.data.@this? ParamByName(
        global::app.goals.goal.steps.step.actions.action.@this action,
        string name)
        => action.Parameters?.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    private static string Normalise(string raw)
        => raw.TrimStart('%').TrimEnd('%').ToLowerInvariant();

    private static string ElementOf(string collectionType)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            collectionType, @"^list<(.+)>$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : "object";
    }

    private static string DetermineReturnType(
        global::app.goals.goal.steps.step.actions.action.@this action,
        modules.@this modules)
    {
        var type = modules.GetActionType(action.Module, action.ActionName);
        if (type == null) return "object";

        var runMethod = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Instance, System.Type.EmptyTypes);
        if (runMethod == null) return "object";

        var returnType = runMethod.ReturnType;
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            returnType = returnType.GetGenericArguments()[0];

        // Data<T> → T
        if (returnType.IsGenericType
            && returnType.GetGenericTypeDefinition() == typeof(global::app.data.@this<>))
            returnType = returnType.GetGenericArguments()[0];

        // Bare Data — no static type info; downstream gets "object".
        if (returnType == typeof(global::app.data.@this)) return "object";

        return global::app.types.@this.GetTypeNameStatic(returnType);
    }
}
