namespace app.goal.steps.step.actions.action;

public sealed partial class @this
{
    /// <summary>
    /// Stamps this action's authored parameter values as live templates — the
    /// deterministic half of the template contract (detection is code, not the
    /// LLM). Each slot marks itself via <see cref="global::app.data.@this.Authored"/>;
    /// modifiers recurse. Runs only at the authored seams (.pr load,
    /// <see cref="FromWire"/>, test fixtures authoring actions) — runtime
    /// input never passes here, so a user string <c>"%secret%"</c> is never
    /// stamped and prints literally. Idempotent.
    /// </summary>
    public void StampTemplates()
    {
        foreach (var p in Parameters) p.Authored();
        if (Defaults != null) foreach (var p in Defaults) p.Authored();
        foreach (var m in Modifiers) m.StampTemplates();
    }
}
