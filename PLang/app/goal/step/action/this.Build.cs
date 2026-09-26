namespace app.goal.step.action;

// The action finishes ITSELF at build. The walk is the node's, like Run and Validate: it binds its
// handler (typed views, nothing resolved), lets the handler judge and finish its own properties, then
// walks what it holds — its modifiers, its recovery, the steps of its branch body. The builder calls
// the chain and reacts to what comes back.
public partial class @this
{
    /// <summary>Binds this action's handler and runs its build-time hooks — <c>Validate()</c>, then
    /// <c>Build()</c> — then does the same for every action it holds: one held in a property (a
    /// callback), its modifiers, its recovery, the steps of its branch body. Null when nothing is wrong;
    /// otherwise one error naming this action, with each finding as a cause.
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

        foreach (var property in Property)
            if (property.Value is @this held && await held.Build(context) is { } heldFailed) causes.Add(heldFailed);
        foreach (var modifier in Modifier)
        {
            if (await modifier.Build(context) is { } invalid) causes.Add(invalid);
            // a recovery is what the step runs on error — running this very action again is a retry
            if (modifier.Recovery.Items().Any(Same))
                causes.Add(new global::app.error.Error(
                    $"the Recovery of {modifier.Module}.{modifier.Name} runs {Module}.{Name}, the action it wraps — " +
                    "Recovery holds what the step runs on error", "RecoveryIsTheAction", 400));
        }
        if (await Recovery.Build(context) is { } recovery) causes.Add(recovery);
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

    /// <summary>Freezes the class's <c>[Default]</c> of every property this action does not set — and
    /// of every action it holds (a property's action, its modifiers, their recovery, its branch body) —
    /// into its <c>Default</c> rows, so a built app runs the same on a later runtime that changes a
    /// default. Each value is born as the property's declared type (the slot's, as the formal reader
    /// types a value), not as the CLR value the attribute happens to hold.</summary>
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
                    Value = declared.Type.Create(declared.Default, context),
                });
            }
        foreach (var property in Property)
            if (property.Value is @this held) held.Freeze(context);
        foreach (var modifier in Modifier)
        {
            modifier.Freeze(context);
            foreach (var recovered in modifier.Recovery.Items()) recovered.Freeze(context);
        }
        foreach (var recovered in Recovery.Items()) recovered.Freeze(context);
        for (int i = 0; i < Child.Count; i++)
            foreach (var action in Child[i].Code.Items()) action.Freeze(context);
    }

    // The same action with the same values.
    private bool Same(@this other) =>
        other.Module == Module && other.Name == Name
        && other.Property.Select(p => (p.Name, p.Value?.ToString())).SequenceEqual(Property.Select(p => (p.Name, p.Value?.ToString())));

    /// <summary>Drops what the step didn't need to write — an explicit null on an optional property,
    /// a value equal to its default (the same behaviour either way) — here and in every action this one
    /// holds: a property's action, its modifiers, their recovery, its branch body. A condition's
    /// operand keeps its null: <c>Right=null</c> compares with null.</summary>
    public void Reduce()
    {
        if (Module[Name] is { } catalog)
            Property.Reduce(catalog.Property, string.Equals(Module.Name, "condition", StringComparison.OrdinalIgnoreCase)
                ? new HashSet<string> { "Left", "Right" } : new HashSet<string>());
        foreach (var property in Property)
            if (property.Value is @this held) held.Reduce();
        foreach (var modifier in Modifier)
        {
            modifier.Reduce();
            foreach (var recovered in modifier.Recovery.Items()) recovered.Reduce();
        }
        for (int i = 0; i < Child.Count; i++)
            foreach (var action in Child[i].Code.Items()) action.Reduce();
    }
}
