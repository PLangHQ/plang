# Accessor model

The settled answer to "what is `app.X`, and how do you get one object out of it."

## `app.X` is the collection node

For each concept `X`, `app.X` returns the **collection** — type `X.list.@this`, living at `X/list/this.cs`. It is owned once, on the singleton that owns the concept: most on `app`, channel on `actor`. It is *not* a flat `App<Plural>` property, and it does *not* live on the element (every `new` element would carry its own empty copy — the multiplication that forces the collection above the element).

```
app.goal            → goal.list.@this        the collection
app.goal["Start"]   → goal.@this             select by name
app.goal.list       → IReadOnlyList<goal>    enumerate
app.goal.current    → goal.@this             the goal I'm in (callstack)
```

### Why `app.goal` is not the violation `AppGoals` was

`AppGoals` was `{App}{Plural}` — a property named for a plurality, typed as a plural blob (`goals.@this`) that sat *beside* the entity (`goals/goal/`). `app.goal` is a property named for the *concept* (singular), typed as the collection node that lives *under* the concept (`goal/list/`), with the element at `goal/this.cs`. App must navigate to its subsystems somehow; a singular concept-property pointing at the concept's collection is that navigation. The plurality and the wrapper alias were the smell, not "app has a property here."

This was hard-won: bare `app.goal` cannot *be* the current goal, because then the collection has no home except a second flat property on app (the violation reborn), and no C# trick rescues it — an implicit `operator goal(goal.list)` gives assignment but not member access, so `app.goal.Name` would still resolve against the collection. One clean name per concept = one occupant. `goal` holds the collection; the current rides on it as `.current`.

## There are no entities vs services — only services, some with `.current`

Goal and type are both collections you select from. The only difference: execution flows *through* goals, so there is a meaningful "current goal" (the callstack tracks it: `CallStack.Current.Action.Step.Goal`, AsyncLocal, fork-safe). Nothing is ever *inside* a type — you consult it. So:

- `.current` exists **only** where the execution context carries a current: **goal** yes. **type, channel, event, module, format** no.
- `app.type.of<T>()` exists only where selecting by CLR type is meaningful: **type** yes. (No real caller selects a type by compile-time generic today; every site is a runtime string or reflected `System.Type` — so `of<T>()` is convenience, the indexer `["int"]` and a reverse `Type→name` are the load-bearing operations. See `type-entity.md`.)

This is the litmus the coder applies per subsystem: *does the execution context carry a "current X"?* If yes, add `.current` (it reads context). If no, don't manufacture one.

## Registry = selection + lifecycle; behavior on the element

`X.list.@this` exposes only:

- **selection** — `this[string name]`, and other key indexers where they exist (goal also has `this[path prPath]`).
- **lifecycle** — `Add`, `Remove`, `Contains`, `Clear`.
- **enumeration** — `IReadOnlyList<X> list` (or implement `IEnumerable<X>`).

Nothing else. **Any behavior on the registry that switches on the element's concrete type is misplaced** — it belongs on the element as a virtual member. The litmus for the coder: grep each registry for `is <element>.` downcasts and `switch` on element kind; each hit is behavior to push down.

### Worked example — channel I/O (you own this)

Today `channels.@this` selects by name *and* type-switches *and* does the I/O:

```csharp
// before — channels/this.cs:199
public async Task<data> WriteTextAsync(string channelName, string text, …) {
    var (channel, error) = GetChannel(channelName, requireWrite: true);
    if (channel is channel.stream.@this sc) await sc.WriteTextAsync(text, …);   // ← type-switch = misplaced behavior
    else await channel!.WriteAsync(Ok(text), …);
}
await actor.Channels.WriteTextAsync(Output, "hello");
```

After: the registry only selects; the channel does its own I/O; the switch becomes polymorphism. **And there is no `WriteText` — that decomposes `data`, which breaks data-opacity.** The element exposes only `Write(data)` / `Read()`; text→data construction happens upstream at the data layer.

```csharp
// element: app/channel/this.cs
public abstract Task<data> Write(data value, CancellationToken ct = default);
public abstract Task<data> Read(CancellationToken ct = default);
// stream override owns the stream-optimized path — channel/stream/this.cs

// call site
await actor.channel["output"].Write(someData);
```

`channels.@this`'s `WriteAsync`/`WriteTextAsync`/`ReadTextAsync`/`ReadChannelAsync<T>` (~9 callers) dissolve into `actor.channel[name].Write/Read`. `ReadTextAsync`/`ReadChannelAsync<T>` have no production callers — confirm dead and delete.

## `.current` reads context, it is not stored

```csharp
// on goal.list.@this — current is computed from the callstack, never a field
public goal.@this? current => App.CallStack.Current?.Action?.Step?.Goal;
```

`current` can be null when nothing is executing — that is correct (there *is* no current goal at rest). This is the one place a null is legitimate on the accessor; selection and enumeration are not null. (`app.goal["Start"]` is null until Start.goal loads — that's a miss, governed by the policy below.)

## Index-miss is a defined policy

`app.X["nope"]` must not silently noop and must not hard-throw from the indexer. The miss behavior (error / noop / create-on-demand) is a setting with a chosen default. Mechanically: the indexer returns the element, so "miss = error" surfaces through the returned element's `Write`/`Read`/`Run` returning a typed `data.Fail` (an indexer can't return `data`). The coder decides the concrete carrier with test-designer; the contract is: **miss behavior is configurable and defaulted, never implicit.**

## Module — a service like the rest (not an exception)

`modules/` → `module/`, and it keeps its `this.cs`: `module/this.cs` = `module.@this` is the action registry, reached as `app.module` like every other collection node. (An earlier draft demoted it to `module/registry.cs` off `app.@this`; dropped at Ingi's call — the registry stays a normal node.)

- `app.module["file"]` selects the file module; `app.module.list` enumerates; the 6 registry operations (`GetCodeGenerated`, `Discover`, `Describe`, `Contains`, `Remove`) stay as methods on `module.@this`.
- **No `app.module.current`** — action modules are dispatched, not navigated; nothing is ever "the current module." That is the *only* way module differs from goal, and it's the same way type/channel/event/format differ. So module is not special — it is a no-`.current` service.
- The collision the demote was meant to avoid (a `.list` accessor vs the `list` action module) isn't real: `app.module.list` is a member, `app.module["list"]` is an indexer key — they don't clash.
- Folding `modules/module` (the action module *about* modules) into `module/environment`, and the `app.run`→`environment.run` / `builder.app`→`builder.load` action renames, are noted but **deferred** — PLang action renames, not namespace shape; CLAUDE.md already marks them a deferred pass. Don't bundle them unless Ingi asks.
