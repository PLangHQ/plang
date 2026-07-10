using System.Reflection;
using app.actor.context;

namespace app.module.goal;

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
///   1. `variable.set Name=%x%, Value=%!data%` → previous producing action's return type
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
[Action("getTypes")]
[System.Obsolete("A string-typed shadow of the type system — type/element/return-type discovery moves to the type entities and module views; do not add new callers.")]
public partial class getTypes : IContext
{
    public partial data.@this<global::app.type.clr.@this<global::app.goal.@this>> Goal { get; init; }

    public async Task<data.@this<global::app.type.item.list.@this<global::app.type.item.dict.@this>>> Run()
    {
        var goal = Goal.Clr<global::app.goal.@this>()!;
        var modules = Context.App.Module;

        var perStep = new List<Dictionary<string, string>>(goal.Steps.Count);
        var working = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Tracks the return type of the most recent producing action in the current step's
        // action chain — needed so `variable.set Value=%!data%` can take the prior
        // action's type. Reset at the start of every step (no cross-step %!data%).
        string? chainReturnType = null;

        for (int i = 0; i < goal.Steps.Count; i++)
        {
            var snapshot = new Dictionary<string, string>(working, StringComparer.OrdinalIgnoreCase);
            perStep.Add(snapshot);
            chainReturnType = null;

            foreach (var action in goal.Steps[i].Actions ?? new())
            {
                ProcessAction(action, working, snapshot, ref chainReturnType, modules, Context.App);
            }
        }

        // List indexed by step position — `%variablesByStep[stepResult.index]%` works
        // out of the box without dict-key coercion.
        // Each step's type-map becomes a native dict; the per-step list is list<dict>.
        var rows = perStep.Select(d =>
        {
            var nd = new global::app.type.item.dict.@this(Context);
            foreach (var kv in d) nd.Set(kv.Key, kv.Value);
            return new data.@this("", nd, context: Context);
        });
        return Context.Ok<global::app.type.item.list.@this<global::app.type.item.dict.@this>>(
            new global::app.type.item.list.@this<global::app.type.item.dict.@this>(rows, Context));
    }

    private static void ProcessAction(
        global::app.goal.steps.step.actions.action.@this action,
        Dictionary<string, string> working,
        Dictionary<string, string> currentStepSnapshot,
        ref string? chainReturnType,
        global::app.module.@this modules,
        global::app.@this app)
    {
        if (string.Equals(action.Module, "variable", StringComparison.OrdinalIgnoreCase)
         && string.Equals(action.ActionName, "set", StringComparison.OrdinalIgnoreCase))
        {
            var nameParam = ParamByName(action, "Name");
            var valueParam = ParamByName(action, "Value");
            var typeParam = ParamByName(action, "Type");
            // Authored source forms ride as text — Peek (no parse, build-meta
            // never materializes content) + the value's own string face.
            var rawName = nameParam?.Peek()?.ToString();
            if (!string.IsNullOrEmpty(rawName))
            {
                string type;
                // The Type slot wins — Build()'s stamp (file.read.Build() → {table,csv}) and the
                // user (type) hint (`write to %x%(json)`). Born-native serializes the entity, so
                // read its name from a type.@this / wire dict / bare string.
                string? hinted = TypeNameOf(typeParam?.Peek());
                if (!string.IsNullOrEmpty(hinted))
                {
                    type = hinted!;
                }
                else if (string.Equals(valueParam?.Peek()?.ToString(), "%!data%", StringComparison.OrdinalIgnoreCase))
                {
                    type = chainReturnType ?? "object";
                }
                else
                {
                    type = TypeNameOf(valueParam?.Type) ?? "object";
                }
                // Strongly typed if determinable, else `item`: route a content-format name
                // through the one format→type mapping (json→item, csv→table, txt→text), and
                // fold the legacy universal `object` to `item`.
                working[Normalise(rawName)] = ToValueType(type, app);
            }
            chainReturnType = null;  // variable.set consumed %!data%
            return;
        }

        if (string.Equals(action.Module, "loop", StringComparison.OrdinalIgnoreCase)
         && string.Equals(action.ActionName, "foreach", StringComparison.OrdinalIgnoreCase))
        {
            var collectionParam = ParamByName(action, "Collection");
            var itemNameParam = ParamByName(action, "ItemName");
            var rawItemName = itemNameParam?.Peek()?.ToString();
            if (!string.IsNullOrEmpty(rawItemName))
            {
                var collectionType = "object";
                var collRef = collectionParam?.Peek()?.ToString();
                if (collRef != null && collRef.StartsWith("%"))
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
        // %!data% sees it. Reflection on the handler's Run() method handles the
        // common case (`Task<Data<T>>` → T). For actions returning bare Task<Data> the
        // chain type stays "object" — that's the lower bound, not a regression.
        chainReturnType = DetermineReturnType(action, modules);
    }

    private static global::app.data.@this? ParamByName(
        global::app.goal.steps.step.actions.action.@this action,
        string name)
        => action.Parameters?.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    private static string Normalise(string raw)
        => raw.TrimStart('%').TrimEnd('%').ToLowerInvariant();

    // The PLang type NAME a Type/Value param carries. Born-native serializes the type entity,
    // so the name lives on a type.@this, a `{name, …}` wire dict, or (legacy/hint) a bare string.
    private static string? TypeNameOf(object? typeValue) => typeValue switch
    {
        null => null,
        global::app.type.@this te => te.IsNull ? null : te.Name,
        global::app.type.item.dict.@this nd => nd.Get("name")?.Peek()?.ToString(),
        System.Collections.Generic.IDictionary<string, object?> d
            => d.TryGetValue("name", out var n) ? n?.ToString() : null,
        string s => string.IsNullOrEmpty(s) ? null : s,
        _ => typeValue.ToString(),
    };

    // "Strongly typed if determinable, else item." A content-format name (json/csv/txt/md)
    // resolves through the one format→type mapping to its value type (json→item, csv→table,
    // txt→text); a value-type name passes through; the legacy universal `object` folds to `item`.
    private static string ToValueType(string typeName, global::app.@this app)
    {
        if (string.IsNullOrEmpty(typeName)) return "item";
        var mapped = app.Format.TypeFromExtension(typeName);
        var name = mapped.IsNull ? typeName : mapped.Name;
        return string.Equals(name, "object", StringComparison.OrdinalIgnoreCase) ? "item" : name;
    }

    private static string ElementOf(string collectionType)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            collectionType, @"^list<(.+)>$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : "object";
    }

    private static string DetermineReturnType(
        global::app.goal.steps.step.actions.action.@this action,
        global::app.module.@this modules)
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

        return global::app.type.list.@this.GetTypeNameStatic(returnType);
    }
}
