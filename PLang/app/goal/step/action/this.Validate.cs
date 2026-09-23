namespace app.goal.step.action;

// The action judges ITSELF. Validate runs AFTER construction, so it reads a finished action and
// changes nothing: a name the builder already repaired and a default it already filled are facts
// by the time this runs. Anything that CHANGES the action is construction and belongs to the
// builder; anything that only JUDGES it lives here.
public partial class @this
{
    /// <summary>What is wrong with this action, or null when nothing is. One error naming the
    /// action, with every finding as a cause in its <c>list</c> — the causes are the detail, the
    /// Message is what a reader (and the build's retry prompt) sees.
    /// <para>A verdict the build cannot proceed with is RETURNED. <see cref="Warning"/> is for what
    /// the build proceeds WITH — a repair that happened, a default that was filled — never for
    /// something fatal.</para></summary>
    public async System.Threading.Tasks.Task<global::app.error.IError?> Validate(
        global::app.actor.context.@this context)
    {
        var causes = new System.Collections.Generic.List<global::app.error.IError>();

        // The module is resolved at read — an action that exists carries a real module element, so
        // only the action name can be wrong here. A bad module name never reaches this: it throws
        // at read, which is what tells the LLM it named something that isn't there.
        var element = Module[Name];
        if (element == null)
        {
            var sorted = Utils.StringDistance.OrderBySimilarity(Name, Module.ActionNames);
            causes.Add(new global::app.error.Error(
                $"{Module}.{Name}: module '{Module}' exists but action '{Name}' not found. " +
                $"Did you mean: {string.Join(", ", sorted.Take(5))}?",
                "ActionNotFound", 400));
        }
        else
        {
            // Required-parameter check. A property is required when it is non-nullable and carries
            // no [Default]. The rows are the ONE reflection site — they already drop [Code],
            // capability and host params — so nothing re-reflects the handler here. The LLM omitting
            // a required param is build-breaking: without it the parameter record cannot be built.
            var emitted = new System.Collections.Generic.HashSet<string>(
                System.StringComparer.OrdinalIgnoreCase);
            if (Parameter != null)
                foreach (var p in Parameter) emitted.Add(p.Name);

            foreach (var row in element.Property)
            {
                if (row.Nullable || row.Default != null) continue;
                if (!emitted.Contains(row.Name))
                    causes.Add(new global::app.error.Error(
                        $"{Module}.{Name}: required parameter '{row.Name}' is missing. " +
                        $"Every action must emit all non-nullable, non-default parameters.",
                        "MissingParameter", 400));
            }
        }

        if (causes.Count == 0) return null;
        return new global::app.error.Error(
            string.Join("; ", causes.Select(c => c.Message)), "ActionInvalid", 400)
        {
            Action = this,
            list = causes,
        };
    }
}
