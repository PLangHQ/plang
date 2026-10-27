# Expected: stage 7, the call stack saved as its frames

What I expect the coder to write (shown to Ingi, not the coder; compared when it reports). Decisions 42, 43, 47, 49, 52a, 57, 62, 63, 77, 80–83. Members not quoted from code are marked NEW. Three parts, each its own commit and review.

## 7a. The frame kinds (81), each with its own rest

```csharp
// call/goal/this.cs, call/step/this.cs, call/action/this.cs — NEW, beside call/binding and call/isolated
namespace app.call.step;
public sealed class @this : global::app.call.@this                // a step's frame
{
    internal @this(global::app.type.@this type, global::app.call.list.@this stack, global::app.goal.step.@this step,
        global::app.call.setting.@this setting) : base(…) { }         // the step's frame constructor moves here (call/this.cs:205–207)

    /// <summary>What's left once it has ended: the rest of the list that holds its step (the goal's steps or a body's),
    /// from the step after it.</summary>
    internal override Task<data.@this> Rest(actor.context.@this context, bool again) => …;
}
// call.action: the rest of its step's code, again or from the next action (today's Place >= 0 arm)
// call.goal: nothing, so its caller goes on (today's last arm)
```

- `call.@this.Rest`'s null-field fork (`call/this.Snapshot.cs:172–177`) goes; the base answers Ok, as binding's does.
- `IsGoal` (`call/this.cs:197`) is the goal frame's own answer, not a guess from nulls.
- `call.list.Push(goal|step|action, …)` make their kind (`call/list/this.cs:187–209`).
- `Goal.Resume(context, index)` takes a list position (`goal/this.Resume.cs`). A step's frame hands it the position of its step in the list that holds it, never the goal-wide `Index` (80).
- Boy scout: `step/this.Resume.cs`'s catch goes through `Error.FromException`, not a key from the class name.
- Tests: the known red `AResume_AfterAStepWithABody_GoesOnAtTheStepItStoppedIn` goes green; a step inside an `if` body resumes the rest of its body, then its goal; the surface is re-pinned citing "snapshot 81".

## 7b. The snapshot view's tag, at the one door (82, 83)

```csharp
// View.cs
[AttributeUsage(AttributeTargets.Property)] public sealed class SnapshotAttribute : Attribute { }   // NEW

internal virtual ValueTask Output(type.item.@this value, IWriter writer) => writer.Write(value);     // NEW: the default
// in snapshot : store —
internal override ValueTask Output(type.item.@this value, IWriter writer)
    => <the type tags [Snapshot]> ? <its [Snapshot] members, by the tagged walk> : writer.Write(value);
internal override bool Shows(PropertyInfo property, bool tagged) => …;   // a type tagging [Snapshot]: those; else [Store], not [Sensitive]

// type/item/this.cs:859
public ValueTask Output(IWriter writer) => writer.View.Output(this, writer);      // was writer.Write(this)
```

The tags:

```csharp
// goal/this.cs
[Store, Out, LlmBuilder, Debug, Default, Snapshot] public string Name { … }
[Debug, Snapshot] public path.@this? PrPath => …;
[Store, Out, Debug, Snapshot] public string? Hash { … }
// goal/step/this.cs
[Store, Out, LlmBuilder, Debug, Default, Snapshot] public int Index { … }
// goal/step/action: NEW [Snapshot] Index (its place in its step's code), [Snapshot] on its module's name and its Name
// type/item/content/this.cs:31
[LlmBuilder, Out, Store, Snapshot] public path.@this Path { get; }
```

Read back, each by its own type:
- A goal, through its own `Select` taking its key record: `goal.list.Read(pr.path)`, then `Find(name, top)`, then the hash (49). It's async, as `Select` is.

**The goal's key, after Ingi's 91 (written before the coder reports; not sent):**

```csharp
// goal/this.Item.cs:92 — Select takes either key a goal is named by: an address a step wrote, or a snapshot's record
public static async ValueTask<data.@this<@this>?> Select(item.@this key, actor.context.@this asker)
    => key switch
    {
        text.@this name => await asker.App.goal.list.Find(name.Clr<string>() ?? "", asker.call.Goal),
        goal.key.@this record => await record.Goal(asker),                       // NEW (91)
        _ => null
    };

// goal/key/this.cs — NEW (91): what finds a goal, as a snapshot reads it; it lives until the first typed ask
public sealed class @this : item.@this
{
    [Snapshot] public text Name { get; }     // each member set by its own type (pattern 1): never record["name"]
    [Snapshot] public file Pr { get; }       // made unread from {path}, as 80
    [Snapshot] public text Hash { get; }

    // the goal it names, the live one: its .pr read (the held goal if unchanged), the goal by name in it, the hash checked
    internal async ValueTask<data.@this<goal>> Goal(actor.context.@this asker) { … }   // a changed hash refuses (409), as a frame's does
}
```

What I'll check when it reports:
1. **Pattern 1:** the record is read, mapped and set by the type list's own read for (goal, key), each member by its own type. No hand unpacking by key string anywhere, and the goal's `.pr` reader is untouched (no branch on shape, not (i)).
2. **Pattern 2:** the kind `key` is written once, by whoever knows it writes a found value's key (the snapshot view through goal's type), never a check in the wire on `type == goal`.
3. **Pattern 8:** the finding (Read, Find, hash) lives on the key, not in `Select`. `Select`'s two arms are the lift, where an untyped value meets the typed ask, as `Create`'s `raw is @this` is.
4. **Not (iii):** `data.Value<T>()`'s `T.IsSelected` door (`data/this.cs:620`) unchanged.
5. The found goal is the app's live goal (the same instance `goal.list` holds), and a changed goal without a re-sign is refused, as at the frame (57).
6. The surface pin re-pins citing "snapshot 91"; FoundElsewhereTests' goal-in-a-variable goes green; the `.pr` goldens unmoved.

**After Ingi's 93 (written before the coder reports; not sent):** the read knows its view, and the goal's reader in the snapshot view loads the goal.

```csharp
// the restore reads under the view the bytes were signed for: the snapshot's
new ReadContext(context, …) { View = View.Snapshot }

// goal/serializer/<snapshot reader>.cs — NEW: goal read in the snapshot view: its key {name, pr, hash} in, the live goal out
//   the goal list's goal for (pr, name); a changed hash refused (409); answers a goal, never a dict (pattern 9)
// the dispatch asks the view, as item.Output does on the write (pattern 7): a type's reader for the view it's read in
// the async load: the sync read keeps the key, and the async Decode loads it before it answers (as DeferVerify does)
// step: by its goal and index (goal[index]); action: by its step and index (step.Code[index]), module and name checked
```

What goes: `View.Type`, the writer asking it, `goal.kind.key`, `goal/serializer/Key.cs`, `goal.Select`'s dict arm. Checks: a frame's `Goal` member and a variable's goal come back by the same reader; nothing unpacks a key by its names outside the goal's own reader; the reader answers a goal; Store, Out and Debug reads are unchanged (the `.pr` reader stays goal's reader outside the snapshot view).

**Compared (067c4e5f2, decision 92):** checks 2 (the view writes the kind, through `View.Type`), 4, 5 and 6 hold. Check 2 also found the view coining `key` on every found type (file too): ruled that only a type that holds a `key` kind writes under it. Checks 1 and 3 miss: the key is a plain dict, unpacked in `goal.Select` by its keys' names, and the mapping loop copies the general read. The typed key above goes to Ingi, since it's a new type.
- A step, by its goal and its goal-wide index: one lookup the goal owns, across its bodies (NEW, the coder names it).
- An action, by its place in its step's code, with the module/name check.
- `file`, `url` and `directory`, each made unread from `{path}` by its own `Create` (80).

Tests:
- A goal in a variable comes back as the live goal.
- A file read before the save comes back unread, and reads the disk again.
- A goal's `.pr` and the page/pick goldens don't move.
- Store, Out and Debug writes are unchanged (the existing wire goldens).
- The surface is re-pinned citing "snapshot 83".

## 7c. The stack saved as its frames, read back through the kinds' doors (77, 52a, 62, 63)

```csharp
// call/this.cs — the frame is a tagged bag; its Write override (:33–68) goes
[Out, Debug] id, depth, startedAt, duration, handled, errors, diffs        // the trace (caller and children leave it)
[Store, Out, Debug] Goal, Step, Action                                       // written in a snapshot by their [Snapshot] members
[Store] public IEnumerable<data.@this> Variable { get; private set; }        // NEW (63): its born names; the setter runs the birth's names logic
[Store] property, Tags (private set), Event [Store, Debug], Error              // the error in play
```

- The stack's `Store`: its frames from the root to where the run is (not ended; a running task refused), each written as itself. The whole snapshot is written once at the save (65), so it is the state at that moment.
- The stack's `Start` (corrected after Ingi's 84): read, map, set. Each frame's record is read by the general reader and mapped onto the frame its kind pushes, each tagged member set by its own type (a goal through its `Select`, a step and an action through their owner). No door of the frame's own and no hand-read fields: the serializer maps it.
- The last frame pushed is where the run stopped (`_resumed`).
- `Thrown` (an error's callback chain, `call/list/this.Snapshot.cs:101–115`) saves its chain the same way.
- 42: a snapshot node writes itself as a plain object of its entries; a variable stays a whole Data in a list.
- 43: `app.id` is saved; resume refuses another app's (NEW key).
- Go: the node constructors, `Placed`, `Stored`, `Find`, `Incomplete`'s keys, ids as pointers, `children`, `current`, and `call/this.Snapshot.cs`'s `Store`.

Tests:
- FreshAppGoesOn is green (the depth, flat now).
- A binding's `%item%` and an event handler's `%!event%` come back.
- A frame's `%!call.scope.x%` property comes back.
- A save inside a task is still refused.
- The text has no `children`, `current` or ids.
- The surface is re-pinned citing "snapshot 77, 81".

## After Ingi's 113 (written before the coder reports; not sent)

**2, the goal's key names its `.goal`:**

```csharp
// goal/this.cs:110 — the key's location is the source; the .pr follows from it
[Debug, LlmBuilder, Snapshot] public path.@this? Path { … }                       // + Snapshot
[Debug] public file.@this? Pr => Parent?.Pr ?? _pr;                                 // − Snapshot (derived from Path)

// goal/serializer/Snapshot.cs — case "path" replaces case "pr"
case "path": path = ctx.View.Reader(types[typeof(path.@this)], null, context).Read(ref reader, null, ctx) as path.file.@this; break;
// the goal the load put in the list (View.Load's location arm → goal.Load → goal.list.Load(path), list/this.cs:45)
if (path == null) throw Refused(… "GoalKeyIncomplete", 400);
if (context.App.goal.list[path.Pr]?.Named(name) is not { } goal) throw Refused(… "GoalNotFound", 404);
// hash compared exactly, as now (409)
```
Checks: no new load code (the location arm and `goal.list.Load` already exist); a sub app gets its parent's goal; a setup goal and an unbuilt one refused by the load; FreshAppGoesOn green; the nested-load pin test red and named for what it wants.

**3, the snapshot taken at once:**

```csharp
// app/this.Snapshot.cs — the walk, then written once in the snapshot view: the state at this moment
public async Task<data.@this<snapshot.@this>> Snapshot(actor.context.@this context)                    // was sync
    => … var snapshot = new snapshot.@this(context); var stored = snapshot.Store(this, thrown, context);
       … return context.Ok(await wire.@this.Of(snapshot, View.Snapshot, context));                    // moved from save.cs:19

// module/snapshot/save.cs — signs what app.Snapshot answers; no wire.Of of its own
var taken = await Context.App.Snapshot(Context);
// error/this.cs:161 — taken once, cached, awaited
[LlmBuilder] public async ValueTask<data.@this<snapshot.@this>> Callback() { … }                      // was a property
// channel/type/message/this.cs:21 — the ask holds what app.Snapshot answers
```
Checks: one door writes the snapshot (pattern 7), no part's Store goes async, Call_Restore_* restore from bytes and pass, `%!error.callback%` reads in plang as before; the variables part's per-value copy goes if the write is the copy (stored twice).

**4, a frame's own variables are its parameters:**

```csharp
// call/this.cs — replaces Variable (call/this.cs:274–287 on the wip), which hid item.Variable
/// <summary>The frame's own variables by name (%!call.scope.parameter.order%), as goal.call names them.</summary>
[Store] public dict.@this Parameter { get => …; private set => …; }      // NEW: keyed, each entry by its name; the setter the birth's names logic
```
Checks: written keyed (the snapshot view names a Data by where it is held, View.cs:249–251); the binding's `%item%` and a goal call's parameters come back by name; reflection finds one member named for it; AFramesOwnVariables and AFramesVariable green.

## Not in this stage

Question 3 (a loop goes on at its item; a held call comes back where it was) and 4b (goal, step and module properties).
