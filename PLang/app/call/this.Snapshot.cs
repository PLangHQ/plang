namespace app.call;

public partial class @this
{
    /// <summary>
    /// Writes this frame's own state into <paramref name="snapshot"/>, its node in its stack's tree (the frames it called
    /// are written under it by the stack):
    ///  - <c>id</c> and its <c>type</c> (the kind of frame it is);
    ///  - where it stands: <c>goal</c> (its <c>address</c>, the name a call anywhere in the app reaches it by, and its
    ///    <c>hash</c> — a mismatch on start is the start's answer), <c>step</c> (its index) and <c>action</c> (its
    ///    <c>index</c>, <c>module</c> and <c>name</c>: the start checks the live action is still it, as the hash covers
    ///    step text, not built actions). An action composed in C#, which no step holds, writes no place;
    ///  - its own variables, each the Data whole (a sensitive value left out, as the variable store leaves it), the names
    ///    it was born with, its errors and whether they were handled, the event running in it, its tags and what it keeps;
    ///    a collection only when it holds something (the snapshot's <c>Write</c>).
    /// Not written: its diffs and timing (history, not state: a resumed frame runs from now), its typed items (runtime
    /// only). A restored error answers its key, message, status and causes; it holds no run, so it has no callback.
    /// A goal with no address can't be found again, so its frame answers why.
    /// </summary>
    internal data.@this Store(global::app.snapshot.@this snapshot, actor.context.@this context)
    {
        var types = context.App.type.list;
        void Set(string name, object value) => snapshot.Entries.Set(new data.@this(name, value, context: context));
        // a place is a node of its own: its members held as Data, so they open where they are read
        global::app.snapshot.@this Node(params (string Name, object Value)[] members)
        {
            var place = new global::app.snapshot.@this(context);
            foreach (var (name, value) in members) place.Entries.Set(new data.@this(name, value, context: context));
            return place;
        }

        Set("id", _id);
        Set("type", Type);
        if (Goal != null)
        {
            if (Goal.Address is not { } address)
                return context.Error(new global::app.error.@this(
                    $"goal '{Goal.Name}' has no address a snapshot can find it by again", "GoalHasNoAddress", 400));
            Set("goal", Node(("address", address), ("hash", Goal.Hash ?? "")));
        }
        if (Step != null) Set("step", Step.Index);
        // the module's NAME, not the element: an element is a live graph node whose actions lead back to it
        if (Place >= 0 && Action is { } action) Set("action", Node(("index", Place), ("module", action.Module.Name), ("name", action.Name)));

        // a node of their own, as a context's variables are, so they come back through the snapshot's one door (In)
        var variables = new global::app.snapshot.@this(context);
        foreach (var (name, variable) in _variables)
            if (variable.Item is not global::app.type.item.sensitive.@this) variables.Entries.Set(name, variable.Clone());
        snapshot.Write("variables", variables);
        snapshot.Write("arguments", new global::app.type.item.list.@this(
            _arguments.Select(name => (global::app.type.item.@this)new global::app.type.item.text.@this(name, types[typeof(global::app.type.item.text.@this)])),
            types[typeof(global::app.type.item.list.@this)]));
        snapshot.Write("errors", Errors);
        Set("handled", _handled);
        if (Event != null) snapshot.Entries.Set("event", Event.Clone());
        snapshot.Write("tags", Tags);
        snapshot.Write("property", property.Copy());
        return context.Ok();
    }

    /// <summary>
    /// The half of a stored frame's coming back that waits, before its kind's <c>Create</c> reads <paramref name="node"/>,
    /// the read copy (the stored text is never touched): each entry taken in this frame's context (the snapshot's one
    /// door) and opened once; its names and errors read as typed lists; its goal found by its address through
    /// <c>app.goal</c> (it may load) and checked against the hash it was stored with; and the setting it is born with,
    /// <paramref name="stack"/>'s as it is now. Each is narrowed into the node. A goal that isn't there, or isn't the
    /// one stored, is the answer.
    /// </summary>
    internal static async System.Threading.Tasks.Task<data.@this> Find(global::app.snapshot.@this node,
        global::app.call.list.@this stack, actor.context.@this context)
    {
        // what came through text stays unread until used: each entry taken in this frame's context and opened here once,
        // and so are its place's members (goal, action)
        async System.Threading.Tasks.Task Open(global::app.snapshot.@this stored)
        {
            foreach (var entry in stored.In(context).ToList())
            {
                stored.Entries.Set(entry);
                await entry.Value();
            }
        }
        await Open(node);
        foreach (var place in new[] { "goal", "action" })
            if (node.Entries.Get<global::app.snapshot.@this>(place, context) is { } members) await Open(members);

        var arguments = await node.Read<global::app.type.item.text.@this>("arguments", context);
        if (await arguments.Value() is not { } names) return arguments;
        node.Entries.Set(new data.@this("arguments", names, context: context));
        var errors = await node.Read<global::app.error.@this>("errors", context);
        if (await errors.Value() is not { } recorded) return errors;
        node.Entries.Set(new data.@this("errors", recorded, context: context));

        if (node.Entries.Get<global::app.snapshot.@this>("goal", context) is { } stored)
        {
            if (stored.Entries.Get<global::app.type.item.text.@this>("address", context)?.ToString() is not { } address)
                return context.Error(Incomplete("goal.address"));
            var found = await context.App.goal.Get(address, context);
            if (await found.Value() is not { } goal) return found;
            var hash = stored.Entries.Get<global::app.type.item.text.@this>("hash", context)?.ToString() ?? "";
            if (!string.Equals(goal.Hash ?? "", hash, StringComparison.OrdinalIgnoreCase))
                return context.Error(new global::app.error.@this(
                    $"the stored frame's goal '{address}' is not the one there now: stored hash {hash}, live {goal.Hash}.",
                    "CallbackGoalHashMismatch", 409));
            node.Entries.Set(new data.@this("goal", goal, context: context));
        }
        node.Entries.Set(new data.@this("setting", await stack.Setting(), context: context));
        return context.Ok();
    }

    /// <summary>A stored frame missing what its start needs: the snapshot is not one this runtime wrote, or it was cut.</summary>
    internal static global::app.error.@this Incomplete(string key)
        => new($"a stored frame is missing '{key}' — the snapshot cannot place it.", "CallbackFrameIncomplete", 400);

    // What a stored frame holds at key, as a T; a missing one names itself (a constructor has no answer, so it throws).
    private static T Stored<T>(global::app.snapshot.@this node, string key, actor.context.@this context)
        where T : global::app.type.item.@this
        => node.Entries.Get<T>(key, context) ?? throw new global::app.error.AppException(Incomplete(key));

    // Where a stored frame stood: its goal (found by Find), its step and its action by index — the hash covers the step
    // text, not the built actions, so the action is checked against the module and name it was stored with.
    private static (global::app.goal.@this?, global::app.goal.step.@this?, global::app.goal.step.action.@this?) Placed(
        global::app.snapshot.@this node, actor.context.@this context)
    {
        if (node.Entries.Get<global::app.goal.@this>("goal", context) is not { } goal) return (null, null, null);
        if (node.Entries.Get<global::app.type.item.number.@this>("step", context) is not { } stepIndex) return (goal, null, null);
        // a step's index is goal-wide, a body's steps numbered on from the step that holds them
        var at = stepIndex.ToInt32();
        var step = goal.Step.At(at) ?? throw new global::app.error.CallbackGoalNotFound($"{goal.Address} (no step {at})");
        if (node.Entries.Get<global::app.snapshot.@this>("action", context) is not { } stored) return (goal, step, null);
        var place = Stored<global::app.type.item.number.@this>(stored, "index", context).ToInt32();
        if (place < 0 || place >= step.Code.Count)
            throw new global::app.error.CallbackGoalNotFound($"{goal.Address} (action {place} out of range at step {step.Index})");
        var action = step.Code[place];
        var module = Stored<global::app.type.item.text.@this>(stored, "module", context).ToString();
        var name = Stored<global::app.type.item.text.@this>(stored, "name", context).ToString();
        if (!string.Equals(action.Module.Name, module, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(action.Name, name, StringComparison.OrdinalIgnoreCase))
            throw new global::app.error.CallbackActionMismatch(goal.Name, step.Index, place, $"{module}.{name}",
                $"{action.Module.Name}.{action.Name}");
        return (goal, step, action);
    }

    /// <summary>The pure core — a frame passes through; nothing else converts into one.</summary>
    public static @this? Create(object? raw) => raw as @this;

    /// <summary>A frame passes through; a stored frame comes back through its own kind's Create (a goal's, a step's, an
    /// action's, a binding's, an isolated one), the type it was stored as.</summary>
    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data) => raw as @this;

    /// <summary>Whether it has ended — a frame kept among its caller's children after that is history, not state.</summary>
    internal bool Ended => _ended;

    /// <summary>Whether it is one of <paramref name="stack"/>'s frames — a task's first frame sits among its caller's
    /// children but belongs to the task's own stack.</summary>
    internal bool Of(global::app.call.list.@this stack) => ReferenceEquals(_stack, stack);

    /// <summary>
    /// What is left of this frame's run once it has ended and what it called has returned, run in its caller — the
    /// call stack's resume goes outward from the frame the run stopped in, each frame in turn doing its own rest
    /// (<paramref name="again"/>: this is the frame the run stopped in). A frame of its own kind says what its rest is
    /// (an action's, a step's); any other leaves nothing — its caller goes on.
    /// </summary>
    internal virtual System.Threading.Tasks.Task<data.@this> Rest(actor.context.@this context, bool again)
        => System.Threading.Tasks.Task.FromResult(context.Ok());
}
