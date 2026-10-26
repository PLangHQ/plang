namespace app.call;

public partial class @this
{
    /// <summary>
    /// Writes this frame's own state into <paramref name="snapshot"/>, its node in its stack's tree (the frames it called
    /// are written under it by the stack):
    ///  - <c>id</c> and its <c>type</c> (the kind of frame it is);
    ///  - where it stands: its goal by its address (the name a call anywhere in the app reaches it by) and hash — a
    ///    mismatch on start is a hard error — its step's index and its action's index, module and name (the start checks
    ///    the live action is still it: the hash covers step text, not built actions). An action composed in C#, which no
    ///    step holds, writes no place;
    ///  - its own variables, each the Data whole (a sensitive value left out, as the variable store leaves it), the names
    ///    it was born with, its errors and whether they were handled, the event running in it, its tags and what it keeps.
    /// Not written: its diffs and timing (history, not state: a resumed frame runs from now), its typed items (runtime only).
    /// A goal with no address can't be found again, so its frame answers why.
    /// </summary>
    internal data.@this Store(global::app.snapshot.@this snapshot, actor.context.@this context)
    {
        var types = context.App.type.list;
        void Set(string name, object value) => snapshot.Entries.Set(new data.@this(name, value, context: context));

        Set("id", _id);
        Set("type", Type);
        if (Goal != null)
        {
            if (Goal.Address is not { } address)
                return context.Error(new global::app.error.@this(
                    $"goal '{Goal.Name}' has no address a snapshot can find it by again", "GoalHasNoAddress", 400));
            Set("goal", address);
            Set("goalHash", Goal.Hash ?? "");
        }
        if (Step != null) Set("stepIndex", Step.Index);
        if (Place >= 0)
        {
            Set("actionIndex", Place);
            // the module's NAME, not the element: an element is a live graph node whose actions lead back to it
            Set("actionModule", Action!.Module.Name);
            Set("actionName", Action.Name);
        }

        // a node of their own, as a context's variables are, so they come back through the snapshot's one door (Held)
        var variables = new global::app.snapshot.@this(context);
        foreach (var (name, variable) in _variables)
            if (variable.Item is not global::app.type.item.sensitive.@this) variables.Entries.Set(name, variable.Clone());
        Set("variables", variables);
        Set("arguments", new global::app.type.item.list.@this(
            _arguments.Select(name => (global::app.type.item.@this)new global::app.type.item.text.@this(name, types[typeof(global::app.type.item.text.@this)])),
            types[typeof(global::app.type.item.list.@this)]));
        if (Errors.CountRaw > 0) Set("errors", Errors);
        Set("handled", _handled);
        if (Event != null) snapshot.Entries.Set("event", Event.Clone());
        Set("tags", Tags);
        Set("kept", _kept.Dict(types));
        return context.Ok();
    }

    /// <summary>
    /// The half of a stored frame's coming back that waits, before its kind's <c>Create</c> reads <paramref name="node"/>:
    /// its goal found by its address through <c>app.goal</c> (it may load) and checked against the hash it was stored
    /// with, and the setting it is born with, <paramref name="stack"/>'s as it is now — both narrowed into the node, the
    /// read copy; the stored text is never touched. A goal that isn't there, or isn't the one stored, is thrown
    /// (<see cref="global::app.error.CallbackGoalNotFound"/>, <see cref="global::app.error.CallbackGoalHashMismatch"/>).
    /// </summary>
    internal static async System.Threading.Tasks.Task Find(global::app.snapshot.@this node, global::app.call.list.@this stack,
        actor.context.@this context)
    {
        // each entry as this frame's context holds it (the snapshot's one door), opened here once — what came through text
        // stays unread until used — so Create reads it as it is, born in its own actor's context
        foreach (var entry in node.Held(context).ToList())
        {
            node.Entries.Set(entry);
            await entry.Value();
        }
        if (node.Entries.Get("goal", context) is { } stored)
        {
            var address = (await stored.Value<global::app.type.item.text.@this>())?.ToString()
                ?? throw new global::app.error.CallbackFrameIncomplete("goal");
            var found = await context.App.goal.Get(address, context);
            var goal = (found.Success ? await found.Value() : null) ?? throw new global::app.error.CallbackGoalNotFound(address);
            var hash = (await (node.Entries.Get("goalHash", context)
                ?? throw new global::app.error.CallbackFrameIncomplete("goalHash")).Value<global::app.type.item.text.@this>())?.ToString() ?? "";
            if (!string.Equals(goal.Hash ?? "", hash, StringComparison.OrdinalIgnoreCase))
                throw new global::app.error.CallbackGoalHashMismatch(address, hash, goal.Hash ?? "");
            node.Entries.Set(new data.@this("goal", goal, context: context));
        }
        node.Entries.Set(new data.@this("setting", await stack.Setting(), context: context));
    }

    // What a stored frame holds at key, as a T; a missing one names itself.
    private static T Stored<T>(global::app.snapshot.@this node, string key, actor.context.@this context)
        where T : global::app.type.item.@this
        => node.Entries.Get<T>(key, context) ?? throw new global::app.error.CallbackFrameIncomplete(key);

    // Where a stored frame stood: its goal (found by Find), its step and its action by index — the hash covers the step
    // text, not the built actions, so the action is checked against the module and name it was stored with.
    private static (global::app.goal.@this?, global::app.goal.step.@this?, global::app.goal.step.action.@this?) Placed(
        global::app.snapshot.@this node, actor.context.@this context)
    {
        if (node.Entries.Get<global::app.goal.@this>("goal", context) is not { } goal) return (null, null, null);
        if (node.Entries.Get<global::app.type.item.number.@this>("stepIndex", context) is not { } stepIndex) return (goal, null, null);
        var at = stepIndex.ToInt32();
        if (at < 0 || at >= goal.Step.Count)
            throw new global::app.error.CallbackGoalNotFound($"{goal.Address} (stepIndex {at} out of range)");
        var step = goal.Step[at];
        if (node.Entries.Get<global::app.type.item.number.@this>("actionIndex", context) is not { } actionIndex) return (goal, step, null);
        var place = actionIndex.ToInt32();
        if (place < 0 || place >= step.Code.Count)
            throw new global::app.error.CallbackGoalNotFound($"{goal.Address} (actionIndex {place} out of range at step {step.Index})");
        var action = step.Code[place];
        var module = Stored<global::app.type.item.text.@this>(node, "actionModule", context).ToString();
        var name = Stored<global::app.type.item.text.@this>(node, "actionName", context).ToString();
        if (!string.Equals(action.Module.Name, module, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(action.Name, name, StringComparison.OrdinalIgnoreCase))
            throw new global::app.error.CallbackActionMismatch(goal.Name, step.Index, place, $"{module}.{name}",
                $"{action.Module.Name}.{action.Name}");
        return (goal, step, action);
    }

    /// <summary>The pure core — a frame passes through; nothing else converts into one.</summary>
    public static @this? Create(object? raw) => raw as @this;

    /// <summary>A frame passes through; a stored frame's node (its async half done, <see cref="Find"/>) comes back as a
    /// goal's, a step's or an action's frame, under the current frame of the stack <paramref name="data"/>'s context
    /// calls.</summary>
    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data) => raw switch
    {
        @this frame => frame,
        global::app.snapshot.@this node => new @this(node, type ?? data.Context!.App.type.list[typeof(@this)], data.Context!.call, data.Context),
        _ => null,
    };

    /// <summary>Whether it has ended — a frame kept among its caller's children after that is history, not state.</summary>
    internal bool Ended => _ended;

    /// <summary>Whether it is one of <paramref name="stack"/>'s frames — a task's first frame sits among its caller's
    /// children but belongs to the task's own stack.</summary>
    internal bool Of(global::app.call.list.@this stack) => ReferenceEquals(_stack, stack);

    /// <summary>
    /// What is left of this frame's run once it has ended and what it called has returned, run in its caller — the
    /// call stack's resume goes outward from the frame the run stopped in, each frame in turn:
    ///  - an action's frame: the rest of its step's actions — its own again when it is where the run stopped
    ///    (<paramref name="again"/>: the ask takes its answer), else from the one after it (the call it made returned);
    ///  - a step's frame: the rest of its goal's steps;
    ///  - a goal's frame, or an action composed in C#: nothing — its caller goes on.
    /// </summary>
    internal virtual System.Threading.Tasks.Task<data.@this> Rest(actor.context.@this context, bool again)
    {
        if (Place >= 0) return Step!.Resume(context, again ? Place : Place + 1);
        if (Step != null && Action == null) return Goal!.Resume(context, Step.Index + 1);
        return System.Threading.Tasks.Task.FromResult(context.Ok());
    }
}
