namespace PLang.Tests.Shared;

/// <summary>
/// Concise goal construction for tests — instead of the nested
/// <c>new Goal { Steps = new GoalSteps { new Step { Actions = new StepActions {
/// new PrAction { Parameters = ... } } } } }</c>, write:
///
/// <code>
/// var goal = Make.Goal("MyGoal",
///     Make.Step("write out %name%",
///         Make.Action("output", "write", ("Content", "Hi %name%"))),
///     Make.Step("if it matches",
///         Make.Action("condition", "if", ("Left", "%x%"), ("Operator", "="), ("Right", 1))));
/// </code>
///
/// Step <c>Index</c> is assigned by position; <c>Path</c> defaults to
/// <c>/{name}.goal</c>. Pair it with <see cref="RealGoalLoad.ViaChannel"/> to load
/// the goal through the real read path.
/// </summary>
public static class Make
{
    /// <summary>A step spec — text + indent + actions. Indexed when the goal is built.</summary>
    public readonly record struct StepDef(
        string Text, int Indent, global::app.goal.steps.step.actions.action.@this[] Actions);

    public static StepDef Step(string text, params global::app.goal.steps.step.actions.action.@this[] actions)
        => new(text, 0, actions);

    /// <summary>A nested step — <paramref name="indent"/> &gt; 0 makes it a child of the
    /// preceding shallower step (orchestration / branch bodies depend on indent).</summary>
    public static StepDef Step(string text, int indent, params global::app.goal.steps.step.actions.action.@this[] actions)
        => new(text, indent, actions);

    /// <summary>
    /// An action with its parameters. A parameter's <b>type comes from its value</b>
    /// (born-typed, like the runtime): <c>("Count", 5)</c> → number, <c>("On", true)</c>
    /// → bool, <c>("Content", "Hi %name%")</c> → text. To <b>declare</b> a type that
    /// differs from the value's natural one — a write-target <c>variable</c>, a date
    /// written as a string, etc. — use <see cref="Param"/>: <c>Make.Param("Name",
    /// "relative", "variable")</c>.
    /// </summary>
    public static global::app.goal.steps.step.actions.action.@this Action(
        string module, string actionName, params (string name, object? value)[] parameters)
    {
        var action = new global::app.goal.steps.step.actions.action.@this
        {
            Module = module,
            ActionName = actionName,
        };
        foreach (var (name, value) in parameters)
            // Param(...) hands back a ready Data carrying an explicit type; a plain
            // tuple value borns its natural type.
            action.Parameters.Add(value is global::app.data.@this typed
                ? typed
                : new global::app.data.@this(name, value, context: global::PLang.Tests.TestApp.SharedContext));
        return action;
    }

    /// <summary>
    /// A parameter with an explicitly-declared type — used inside
    /// <see cref="Action"/>'s parameter list when the declared type differs from the
    /// value's natural type (e.g. <c>Make.Param("Name", "relative", "variable")</c>). For
    /// the common case where the value's own type is right, a plain
    /// <c>(name, value)</c> tuple is enough.
    /// </summary>
    public static (string name, object? value) Param(string name, object? value, string type)
        => Param(name, value, new global::app.type.@this(type));

    /// <summary>
    /// A parameter carrying an explicit <paramref name="kind"/> (and optionally
    /// <paramref name="strict"/>) — the build-time refinement a <c>.pr</c> param carries
    /// in <c>{type:{name, kind, strict}}</c>. E.g. <c>Make.Param("type", null, "text",
    /// "md")</c> mirrors <c>as text/md</c>; <c>(…, "image", "gif", strict: true)</c>
    /// mirrors <c>as image/gif strict</c>. Round-trips through the read like any param.
    /// </summary>
    public static (string name, object? value) Param(
        string name, object? value, string type, string kind, bool strict = false)
        => Param(name, value, new global::app.type.@this(type, kind, strict));

    /// <summary>A parameter with a fully-built type entity — for cases that construct
    /// <see cref="global::app.type.@this"/> directly.</summary>
    public static (string name, object? value) Param(
        string name, object? value, global::app.type.@this type)
        => (name, new global::app.data.@this(name, value, type, context: global::PLang.Tests.TestApp.SharedContext));

    /// <summary>A text parameter carrying an interpolation template (an embedded or full
    /// <c>%ref%</c>) — models the builder stamping <c>type.template="plang"</c> on a value
    /// that contains a <c>%var%</c>. The read fills the holes against live variables.</summary>
    public static (string name, object? value) Template(string name, string value)
        => Param(name, value, new global::app.type.@this("text", template: "plang"));

    /// <summary>
    /// Wraps an action with one or more modifier actions (e.g. <c>timeout.after</c>,
    /// <c>error.handle</c>, <c>cache</c>). The modifiers run around the inner action;
    /// each fires its own lifecycle events. Returns the same inner action for nesting
    /// inside <see cref="Step"/>.
    /// </summary>
    public static global::app.goal.steps.step.actions.action.@this Modified(
        global::app.goal.steps.step.actions.action.@this inner,
        params global::app.goal.steps.step.actions.action.@this[] modifiers)
    {
        foreach (var modifier in modifiers)
            inner.Modifiers.Add(modifier);
        return inner;
    }

    /// <summary>
    /// Attaches build-time <b>defaults</b> to an action — the param values a
    /// <c>.pr</c> carries in its <c>defaults</c> list (what the builder captured at
    /// build time). A default applies only when the same-named parameter is absent;
    /// an explicit parameter wins. Born-typed like any param. Returns the same action
    /// for nesting inside <see cref="Step"/>.
    /// </summary>
    public static global::app.goal.steps.step.actions.action.@this WithDefaults(
        global::app.goal.steps.step.actions.action.@this action,
        params (string name, object? value)[] defaults)
    {
        action.Defaults ??= new List<global::app.data.@this>();
        foreach (var (name, value) in defaults)
            action.Defaults.Add(value is global::app.data.@this typed
                ? typed
                : new global::app.data.@this(name, value, context: global::PLang.Tests.TestApp.SharedContext));
        return action;
    }

    public static global::app.goal.@this Goal(string name, params StepDef[] steps)
        => Goal(name, $"/{name}.goal", steps);

    /// <summary>
    /// A goal at an explicit <paramref name="path"/> — for tests where the goal's
    /// folder matters (relative file-path resolution, parent traversal). The path
    /// rides the wire and is restored by the read, exactly like a <c>.pr</c> off disk.
    /// </summary>
    public static global::app.goal.@this Goal(string name, string path, params StepDef[] steps)
    {
        var goal = new global::app.goal.@this
        {
            Name = name,
            Path = global::app.type.path.@this.Resolve(path, global::PLang.Tests.TestApp.SharedContext),
            // Steps need a context so the disabled-step probe (and any enumeration,
            // e.g. PrWrite serialization in RealGoalLoad.ViaChannel) doesn't deref a
            // null context. Transient like the param births — the real actor context
            // is stamped when the goal is read back through a channel.
            Steps = new global::app.goal.steps.@this { Context = global::PLang.Tests.TestApp.SharedContext },
        };

        for (int i = 0; i < steps.Length; i++)
        {
            var actions = new global::app.goal.steps.step.actions.@this();
            foreach (var action in steps[i].Actions)
                actions.Add(action);

            goal.Steps.Add(new global::app.goal.steps.step.@this
            {
                Index = i,
                Indent = steps[i].Indent,
                Text = steps[i].Text,
                Actions = actions,
            });
        }

        return goal;
    }
}
