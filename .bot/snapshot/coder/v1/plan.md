# snapshot — coder v1: push-back and stage 1's shape

Read: the plan (`.bot/snapshot/architect/plan/start.md`), `snapshot/{ISnapshot,this,this.Wire,this.Resume,this.Variables}.cs`,
`app/this.Snapshot.cs`, `call/list/this.Snapshot.cs`, `call/this.Snapshot.cs`, `call/Position.cs`, `call/this.cs`,
`call/list/this.cs`, `module/code/this.Snapshot.cs`, `type/item/kept/list/this.cs`, `variable/list/this.Snapshot.cs`,
`app/this.cs`, `actor/list/this.cs`, `type/node/{this,current/this}.cs`, `type/list/this.cs:311–344` (plang's member list),
`View.cs`.

## Push-back on the plan

1. **Plang's tree doesn't reach the parts.** The plang tree (`type.list.Member(clr)`, the `[LlmBuilder]` members a value
   reads through `property.Read`) reaches only the call stack (`app.call(context)`, `app/this.cs:110–111`), and only as the
   asker's. It never reaches:
   - `Code` (`app/this.cs:159`, no tag: C# plumbing);
   - the app's kept (`_kept`, private, behind `internal virtual Kept`, `item/this.cs:232`);
   - the variable store (`context.Variable`): `app.variable` is a `type.node` over variables, not the store (`app/this.cs:219`).

   So "walk the app tree" has to be the C# member tree, not plang's (shape below). A part's place is its C# member path from
   `app`, e.g. `actor.list.User.Context.Variable`.
2. **Two contexts, not one.** Today the snapshot takes the asker's variables and call stack (`app/this.Snapshot.cs:16–17`).
   A walk from `app` reaches both actors' contexts (`actor.list.System`, `.User`, `actor/list/this.cs:21–24`), each with its
   own `Variable` and `call`. "Everything": both are stored, both start, and the run goes on in the context whose call stack
   has a current. A task's context (a parallel branch, `call/list/this.cs` "a task's context has its own chain") is no
   member of anything, so the walk never reaches it. **A save inside a parallel branch saves only the main chain.** Needs a
   ruling: refuse a save inside a task, or accept it as it is.
3. **"Started exactly" stops at an action that is mid-call.** The C# async stack can't be restored. An outer frame's action
   (`goal.call`, `loop.foreach`, an `if` that calls, an event's bound call) is in flight, and today it resumes at
   `ActionIndex + 1` (`snapshot/this.Resume.cs:45`).
   - For `goal.call` that is right.
   - For a `foreach` saved in its 2nd of 5 items it is wrong: items 3–5 are skipped (the loop's index is C# local state, on
     no frame).
   - Exact means the action that called goes on itself from what its frame holds: the binding frame holds `%item%`, so it
     could hold its position too.

   That is a door on a calling action ("go on from this frame"), stage 2's scope. **Needs a ruling:** exact for the
   loops, or "after the action" for every action, documented as not exact for loops.
4. **The error's throw-time snapshot** (`app/this.Snapshot.cs:25–26`, used by `error.Callback`, `error/this.cs:164`) puts
   two stand-ins in for the live parts: `Variable.SnapshotAt(error)` and `call.At(error.CallFrames)`. A walk over the live
   tree has no seam for that. The plan says it "keeps its throw-time projection through the new door". Options:
   - (a) it stays a second door until the callback plan (callbacks are later anyway);
   - (b) the walk takes the error and the two parts store from it (`Store(node, error?)`), which widens ISnapshot for one
     user.

   I recommend (a).
5. **The three ask tests** (`test/plan/snapshot/module/callback/`): "move out to a callback list". There is no callback
   plan folder, and `test/plan/` holds only plans in progress. **Where does the list live?** Until you say, I leave them
   where they are.
6. **`kept` is no part.** The app's kept is the app's own state, so the app's `Store` writes it beside its mode, and
   `kept.list` loses `ISnapshot`. A frame's kept (`call/this.cs:20`) is the frame's, written by the frame in stage 2. This
   keeps "a class that takes part" true: the kept list is not a thing in the tree, it is a member of an owner.

## Stage 1's shape

### `ISnapshot` (`app/snapshot/ISnapshot.cs`), two members

```csharp
public interface ISnapshot
{
    /// <summary>Writes this part's own state into <paramref name="node"/>, its place in the snapshot: its entries, never its
    /// parts (the walk stores those at their own places).</summary>
    void Store(snapshot.@this node);

    /// <summary>Comes back from what it stored in <paramref name="node"/>: hard errors on what can't come back (a goal gone,
    /// a DLL that won't load), no fallback.</summary>
    System.Threading.Tasks.Task Start(snapshot.@this node, actor.context.@this context);
}
```

- `Store` writes into the node the walk made for it, through the members a part writes with today:
  - `snapshot.Write<T>(key, value)` (`snapshot/this.cs:69–71`), which births a plang value at the write door;
  - `snapshot.Entries.Set(data)` (a Data whole, as Variables does, `variable/list/this.Snapshot.cs:32`).
- `Start` reads through `snapshot.Read<T>(key)` (`snapshot/this.cs:75–83`) and `Entries.Get(key, context)` (`:61`, dict).
- It stays sync to store: every part's state is in memory, and Data clones are sync. `Section`, `Capture` and `Restore` go.

### The walk: `snapshot.save` → the snapshot

- **Where it lives:** the walk is the snapshot's own birth from the app: `snapshot.@this.Save(app, context)`, or a
  constructor. The `snapshot.save` action answers it.
- **Which nodes it visits:** from `app`, the public instance properties whose **declared type** implements `ISnapshot`, and
  from each such node the same, depth first, in declaration order. A member whose declared type is no part is never read,
  so its getter never runs (`store`'s lazy open, `Services`, `goal.list`'s reading stay untouched).
- **How it finds a part without a list or a switch:** the classification is the interface, as `ISnapshot`'s doc already says
  ("the type system is the classifier"). One test per member, `typeof(ISnapshot).IsAssignableFrom(property.PropertyType)`,
  and no switch on what the part is. A node is stored by `part.Store(node)`. Its children are visited by the same rule.
- **The holders on the way:** to reach `actor.list.User.Context.Variable`, each node on the path must be an ISnapshot whose
  `Store` writes nothing of its own:
  - `type.node.current<actor, actor.list>` (app's `actor`, `app/this.cs:262`);
  - `actor.list`, `actor`, `actor.context`.

  The node's `list` is `L`, declared `actor.list`, so it is a part. `current(context)` is a method, so it is not walked
  (it is the asker's, not a holding). For the other nodes (`goal`, `test`, `error`), `list` is declared `goal.list` etc.,
  no part, so it is never read. **`type.node` is the type system's: making it take part is a locked addition** (below).
- **A part's place is its path:** the walk makes each child node with the existing child-by-name door
  (`snapshot.Section(name)`, `snapshot/this.cs:39–45`: "a section is an entry whose value is a snapshot"), named the
  member's name. So the saved tree is

  ```
  { mode, kept,                                   // the app's own (app is the root part)
    Code: { registrations, defaultOverrides },
    actor: { list: { System: { Context: { Variable: {…}, call: {…} } },
                     User:   { Context: { Variable: {…}, call: {…} } } } } }
  ```

  and `%snap.actor.list.user.context.variable.x%` navigates to a saved variable, the same door as today
  (`snapshot/this.Variables.cs:12–13`).
- **Start walks the saved tree** in the same order: for each part the walk reaches on the live app, its node by the same
  name, if saved. So the order is root first (mode, which sets `Build`/test session), then `Code` (providers, declared at
  `:159`) before `actor` (`:262`), the order today's list keeps (providers first). A part that has to start before another
  depends on declaration order, which is implicit; I'd pin it with a test rather than a rule.
- **Code's DLL:** `Start` is async, so `await dllPath.LoadAssemblyAsync(context)`, and the `.GetAwaiter().GetResult()`
  (`module/code/this.Snapshot.cs:116`) goes.

## Where the call stack's exact restore touches the locked call memory

Stage 2. `call.list` and `call` are locked ("call and variable memory").

1. **`call.list` takes part** with `Store`/`Start`, which replace its public `Section`/`Capture`/`Restore`
   (`call/list/this.Snapshot.cs:116–232`). This renames public members of a locked type, so the surface golden is re-pinned.
   It drops `RestoredChain`, `BottomFrame`, `At`, `Thrown` (if the error path goes to (a)) and `Position`.
2. **`Start` installs a tree:** it sets `_root` and `_current.Value` (the AsyncLocal) and fills each frame's `Children`.
   `Enter(frame)` (`call/list/this.cs:229–235`) does this one frame at a time from the live current. A restore needs it from
   stored parents: private members, inside the class, no surface change.
3. **A frame born from what it stored:** today's constructors mint a new `_id`, take `stack.Current` as caller and
   `_previousCurrent`, and compute `_depth` (`call/this.cs:216–263`). A stored frame needs its `_id`, its caller (its stored
   parent), its own `_variables`/`_arguments` (`:179–180`), `Errors`+`Handled` (`%!error%` reads them), `Tags`, `Event`
   (`%!event%` in a bound call), `_for` (a tool's held action) and its kept. That is a new internal constructor. The kind of
   frame (`call`, `binding`, `isolated`) is its type, so it is read back through its type's `Create` (`ICreate`), never a
   switch.
   - **Is an internal constructor on `call` a locked-surface addition, or a private refactor?** I read "inside them,
     behaviour fixes and private refactors only" as allowing it (not a member others call).
   - **Its subclasses each answering `Create` is an interface added to three locked types:** I think that is an addition,
     for Ingi.
4. **Going on from current with no recursion:** a loop from current outward. It runs the current frame's goal from its
   action; when that returns, the frame is disposed (`call/this.cs:436–454`, which restores the current and drops it from
   its caller's children) and the caller's action goes on. If the loops are exact (push-back 3), that last step is a door on
   the calling action, not on call memory.
5. **Not stored:** `Diffs` and the diff stream (history, not state), the timing (`_startedAt`/`_stopwatch`: a resumed frame
   runs from now), and `_items` (the typed metadata bag, `call/this.cs:335–349`: runtime only, as today).

## Locked additions to bring Ingi

- `type.node` (generic, `type/node/this.cs:20`) implementing `ISnapshot`, as a holder with nothing of its own (stage 1).
- `call.list`'s public `Section`/`Capture`/`Restore` become `Store`/`Start`; `RestoredChain`, `BottomFrame` and `At` go
  (stage 2).
- `call`, `binding` and `isolated` each read back through their type's `Create` (`ICreate`) (stage 2).
- `actor.list`, `actor` and `actor.context` implementing `ISnapshot`: `actor` is core and its context holds the locked call
  and variable memory.

## Open, needing a ruling

- (2) a save inside a parallel branch;
- (3) exact loops, or "after the action";
- (4) the error's throw-time snapshot, (a) or (b);
- (5) where the callback list lives.
