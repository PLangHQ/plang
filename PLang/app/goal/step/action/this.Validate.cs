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
    public async System.Threading.Tasks.Task<global::app.error.Error?> Validate(
        global::app.actor.context.@this context)
    {
        var causes = new System.Collections.Generic.List<global::app.error.Error>();

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
            // The program action's properties against its catalog twin's rules. The LLM omitting a
            // required property is build-breaking. A property the class does not declare is a name
            // the LLM made up.
            foreach (var declared in element.Property)
            {
                if (!declared.Required) continue;
                if (Property[declared.Name] == null)
                    causes.Add(new global::app.error.Error(
                        $"{Module}.{Name}: required property '{declared.Name}' is missing. " +
                        $"Every action must emit all non-nullable, non-default properties.",
                        "MissingProperty", 400));
            }
            foreach (var property in Property)
            {
                if (element.Property[property.Name] is not { } slot)
                    causes.Add(new global::app.error.Error(
                        $"{Module}.{Name}: '{property.Name}' is not a property of this action. " +
                        $"Its properties are: {string.Join(", ", element.Property.Select(p => p.Name))}.",
                        "UnknownProperty", 400));
                // an action held as a value belongs only in a slot that takes actions (action, list<action>)
                else if (property.Value is @this held && !(slot.Type.Name == "action"
                         || (slot.Type.Name == "list" && slot.Type.Kind?.Name == "action")))
                    causes.Add(new global::app.error.Error(
                        $"{Module}.{Name}'s {property.Name} holds an action ({held.Module.Name}.{held.Name}), but " +
                        $"{property.Name} takes a value. Write the action first, then {Module}.{Name}({property.Name}=%!data%).",
                        "ActionAsValue", 400));
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
