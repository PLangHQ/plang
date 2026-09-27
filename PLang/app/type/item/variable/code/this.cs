namespace app.type.item.variable.code;

/// <summary>
/// A variable's code: its hops in order, the root first. Each hop gets what the one before it
/// answered and does its one step. Program structure — born with no context, shared by every run;
/// <see cref="Start"/> and <see cref="Set"/> take the asker's.
/// </summary>
public sealed class @this : global::app.type.item.list.@this<Hop>
{
    internal @this(System.Collections.Generic.IEnumerable<Hop> hops) : base(hops) { }

    protected override global::app.type.item.list.@this Empty() => new @this([]);

    /// <summary>Its <c>.pr</c> form: the hops in order, each under its kind.</summary>
    public override void Write(global::app.channel.serializer.IWriter writer)
    {
        var hops = Items().ToList();
        writer.BeginArray(hops.Count);
        foreach (var hop in hops) hop.Write(writer);
        writer.EndArray();
    }

    public override System.Threading.Tasks.ValueTask Output(global::app.channel.serializer.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        Write(writer);
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }

    /// <summary>The root: the name the value lives under in memory.</summary>
    public Variable Root => (Variable)this[0];

    /// <summary>Runs every hop: what the variable holds.</summary>
    public async System.Threading.Tasks.ValueTask<global::app.data.@this> Start(global::app.actor.context.@this context)
    {
        global::app.data.@this? current = null;
        foreach (var hop in Items())
        {
            current = await hop.Start(current, context);
            if (!current.IsInitialized || !current.Success) return current;
        }
        return current!;
    }

    /// <summary>Runs every hop but the last to reach the parent, and the last writes itself: a member
    /// sets that member, an index that key, a <c>!</c> name the binding's Properties, a bare root
    /// rebinds the variable. A root that holds nothing becomes an empty dict when a member or key is
    /// written into it; a <c>!</c> name written straight on the root needs the variable to exist.</summary>
    public async System.Threading.Tasks.ValueTask<global::app.data.@this> Set(object? value, global::app.actor.context.@this context)
    {
        var hops = Items().ToList();
        if (hops.Count == 1) return await Root.Set(null, value, context);

        var parent = hops is [_, Property { IsBinding: true }]
            ? await Root.Start(null, context)
            : await Root.Ensure(context);
        for (int i = 1; i < hops.Count - 1; i++)
        {
            parent = await hops[i].Start(parent, context);
            if (!parent.IsInitialized || !parent.Success) return parent;
        }
        return await hops[^1].Set(parent, value, context);
    }
}
