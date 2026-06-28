namespace app.goal.steps.step.actions.action;

public sealed partial class @this
{
    /// <summary>
    /// DEPRECATED. Eagerly stamped this action's authored parameters as live templates.
    /// Superseded by the read-time template mechanism: %ref% holes ride as live templates
    /// stamped on READ via <c>ctx.Template</c> (the Wire's <c>ReadOptions</c>), so no eager
    /// pass is needed. Existing callers are legacy to migrate; add no new ones.
    /// </summary>
    [System.Obsolete("Templates are stamped on read via ctx.Template (Wire.ReadOptions), not eagerly. " +
        "Do not add new callers; existing ones are legacy to migrate.")]
    public void StampTemplates()
    {
#pragma warning disable CS0618 // deprecated mechanism calling its own deprecated half
        foreach (var p in Parameters) p.Authored();
        if (Defaults != null) foreach (var p in Defaults) p.Authored();
        foreach (var m in Modifiers) m.StampTemplates();
#pragma warning restore CS0618
    }
}
