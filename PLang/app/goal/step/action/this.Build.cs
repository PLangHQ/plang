namespace app.goal.step.action;

// The action finishes ITSELF at build. The walk is the node's, like Run and Validate: it binds its
// handler (typed views, nothing resolved), lets the handler judge and finish its own properties, then
// walks what it holds — the actions its properties hold, the steps of its branch body. The builder calls
// the chain and reacts to what comes back.
public partial class @this
{
    /// <summary>The actions this action's properties hold — a callback (<c>action</c>), a recovery
    /// (<c>list&lt;action&gt;</c>) — in property order. Program, walked like the branch body.</summary>
    internal System.Collections.Generic.IEnumerable<@this> Held
    {
        get
        {
            foreach (var property in Property)
                switch (property.Value)
                {
                    case @this held: yield return held; break;
                    case list.@this actions: foreach (var held in actions.Items()) yield return held; break;
                }
        }
    }

    /// <summary>Binds this action's handler and runs its build-time hooks — <c>Validate()</c>, then
    /// <c>Build()</c> — then does the same for every action it holds: one its properties hold (a callback,
    /// a recovery), the steps of its branch body. Null when nothing is wrong; otherwise one error naming
    /// this action, with each finding as a cause.
    /// <para>A handler's <c>Build()</c> result is published as <c>%!buildData%</c> — the handle the
    /// next action's <c>Build()</c> reads to see what it captures (build-scoped, so it never clobbers
    /// the runtime <c>%!data%</c> of the actor running the builder).</para></summary>
    public async System.Threading.Tasks.Task<global::app.error.Error?> Build(
        global::app.actor.context.@this context)
    {
        var causes = new System.Collections.Generic.List<global::app.error.Error>();

        var (handler, bindError) = await Bind(context);
        if (bindError != null)
            causes.Add(bindError);
        else if (handler is global::app.module.IClass own)
        {
            // Each literal must be a value its slot can take — the build names the fix to the LLM.
            var declined = await own.Parse();
            foreach (var decline in declined)
                causes.Add(new global::app.error.Error(
                    $"{decline.Message} Leave it out if the step does not name it — never emit \"\" as a placeholder.",
                    decline.Key, decline.StatusCode));
            // The handler judges and finishes only values its slots could take.
            if (declined.Count == 0)
            {
                if (await own.Validate() is { } complaint)
                    causes.Add(complaint);
                else
                {
                    var built = await own.Build();
                    if (!built.Success) causes.Add(built.Error ?? new global::app.error.Error("Build() failed", "BuildFailed", 400));
                    else await context.Variable.Set("!buildData", built);
                }
            }
        }

        foreach (var held in Held)
            if (await held.Build(context) is { } heldFailed) causes.Add(heldFailed);
        for (int i = 0; i < Child.Count; i++)
            if (await Child[i].Code.Build(context) is { } branch) causes.Add(branch);

        if (causes.Count == 0) return null;
        return new global::app.error.Error(
            $"{Module}.{Name}: {string.Join("; ", causes.Select(c => c.Message))}", "BuildFailed", 400)
        {
            Action = this,
            list = causes,
        };
    }

    /// <summary>A build-time warning about this action — written on the build's "builder" channel as
    /// <c>{action, message}</c>, naming the action; never a refusal. The one door a handler's <c>Build()</c> warns
    /// through.</summary>
    public async System.Threading.Tasks.Task Warn(string message, global::app.actor.context.@this context)
    {
        if (context.Actor.Channel.Get("builder") is { } builder)
            await builder.WriteAsync(context.Ok(new global::app.type.item.dict.@this()
                .Set("action", $"{Module}.{Name}")
                .Set("message", message)));
    }

    /// <summary>Freezes the class's <c>[Default]</c> of every property this action does not set — and
    /// of every action it holds (its properties' actions, its branch body) — into its <c>Default</c> rows, so a
    /// built app runs the same on a later runtime that changes a default. Each value is born as the property's
    /// declared type (the slot's, as the formal reader types a value), not as the CLR value the attribute
    /// happens to hold.</summary>
    public void Freeze(global::app.actor.context.@this context)
    {
        if (Module[Name] is { } catalog)
            foreach (var declared in catalog.Property)
            {
                if (declared.Default == null || this[declared.Name] != null || Default[declared.Name] != null) continue;
                Default.Add(new global::app.type.property.@this
                {
                    Name = declared.Name.ToLowerInvariant(),
                    Type = declared.Type,
                    Value = declared.Type.Make(declared.Default, context),
                });
            }
        foreach (var held in Held) held.Freeze(context);
        for (int i = 0; i < Child.Count; i++)
            foreach (var action in Child[i].Code.Items()) action.Freeze(context);
    }

    /// <summary>The same action with the same values.</summary>
    internal bool Same(@this other) =>
        other.Module == Module && other.Name == Name
        && other.Property.Select(p => (p.Name, p.Value?.ToString())).SequenceEqual(Property.Select(p => (p.Name, p.Value?.ToString())));

    /// <summary>Drops what the step didn't need to write — an explicit null on an optional property,
    /// a value equal to its default (the same behaviour either way) — here and in every action this one
    /// holds: its properties' actions, its branch body. A condition's operand keeps its null: <c>Right=null</c>
    /// compares with null.</summary>
    public void Reduce()
    {
        if (Module[Name] is { } catalog)
            Property.Reduce(catalog.Property, string.Equals(Module.Name, "condition", StringComparison.OrdinalIgnoreCase)
                ? new HashSet<string> { "Left", "Right" } : new HashSet<string>());
        foreach (var held in Held) held.Reduce();
        for (int i = 0; i < Child.Count; i++)
            foreach (var action in Child[i].Code.Items()) action.Reduce();
    }
}
