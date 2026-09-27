# 8b-3: app becomes an item (the architect's own trace)

Written before reading the coder's 8b-3 trace. The comparison is at the end. Ingi, 2026-09-27: "yes on app and channel".

## Why

The app's `on.start` needs `on`, and the event machinery is item-shaped all the way down (`on`, `Own()`, `Level` and a binding's handler all take an item). Actor, goal, module and step are items already. The app is the last plain class that has an event.

## What exists today (read 2026-09-27)

- **The declaration** (`app/this.cs:19`): `sealed partial class @this : IAsyncDisposable, ISetting<app.setting>`. The partials are `this.Snapshot.cs:3` (`ISnapshot`) and `this.SnapshotWire.cs:13`.
- **`%!app%`** is a `DynamicData("!app", () => App)` set in each actor (`actor/this.cs:107`) and each context (`actor/context/this.cs:156`). Its cell is a `computed` whose `Compute` is `item.Create(_factory(), context)` (`type/item/computed.cs:38-39`). Today that wraps the raw app as a `clr` item, and navigation reflects over it. `variable.IsKeyed` (`type/item/variable/this.cs:63-66`) recognises `!app` as a root by name only.
- **Fluid:** `AmbientValues["app"] = action.Context.App` (`ui/code/Fluid.cs:135`), read back by a cast (`:377`). It holds the object, not a door.
- **The entry run** (`app/this.cs:529-593`): `Start()` → `Launch()` → (Build mode → `Build!.Start()`) → resolve `goalFile` → `goal.Load(goalFile)` → `goal.Start(User.Context)`. A failed run goes to `Show`.
- **The item base** (`type/item/this.cs`): abstract with no abstract members. Its defaults: `Output` → `Write(writer)` (:624-630), `Clone` a deep clone (:319), `Level` = `[this]` (:339), `Type => new(GetType())` (:298), navigation through `clr` reflection plus `Setting(parent, key)` for an `ISetting` owner (:236-248). It exposes public `on`, `Template`, `Variable`, `Cacheable`, `Rank`.

## Shape

1. **`app.@this : item.@this, IAsyncDisposable, ISetting<app.setting>`**, with `[PlangType("app")]`. The namespace `app` is its name. No type is named or spelled `app` today, so there's no clash.
2. **Overrides:**
   - `Clone() => this`. The app is one object, so a copy of it is itself, and the deep clone must never walk it (actors, contexts, the store).
   - `Output` goes through the reflection kind, module-style, writing its `[Out]` members. `IsLeaf` stays false.
   - If `ICreate` has to be answered, `Create` declines ("an app is never made from a value"), the way setting's does.
3. **`%!app%` keeps its door.** `computed.Compute` → `item.Create(app)` now passes the item through as it is, with no `clr` wrap. Navigation reaches the same members through item's `Get` (clr reflection) and `Setting` (`%!app.setting%`). `IsKeyed` is unchanged. Two options for the door:
   - (a) keep the `DynamicData`;
   - (b) a plain Data holding the app. The app never changes, so there's nothing to compute fresh. The lambda exists only because a context is born before its `App` is set.
   Keep (a) unless the trace shows `App` is set at birth.
4. **Firing, only around Launch's entry goal** (as decided): `var answer = await on.start.Before(this, User.Context); var result = answer is { Success: false } or { Handled: true } ? answer : await goal.Start(User.Context); return await on.start.After(this, result, User.Context);`. Not in Build mode, not in Show, and not in `Start(Goal, context)` (a C# door that starts a goal already in memory).
5. **Scope:** a binding on the app's start is app-scoped (decision 94), like load. The binding list's scope check already lets an app-scoped binding fire for any actor. 8f's `on.event(item: app, …)` binds with scope `app`.
6. **Levels:** the default `[this]`. There is one app, so a type level would be the same object.
7. **Fluid:** unchanged, since it holds the same object.

## What to watch

- **The item-base surface on the app:** `%!app.template%`, `%!app.variable%`, `%!app.cacheable%` and `%!app.rank%` become reachable by reflection. `%!app.on…%` is the point. The rest is the same leak the catalog just fixed for `signing.sign`. Navigation isn't the catalog, so note it but don't fix it here. **`%!app.variable%` is a real collision:** `variable` is the concept type `app.variable` (`type<variable>`), while item's public `Variable` (the item's variable list) is another member with the same name. Check which one navigation answers. The concept must win.
- **Anything that `is app.@this` or casts raw values** (a `clr` reader expecting the app as `clr`): grep `clr<app>`, `.Clr<app.@this>`, and `is global::app.@this`.
- **Snapshot:** app is an `ISnapshot` (parked). Check that becoming an item doesn't route the app through item's snapshot or wire paths.
- **The wire:** is the app ever written as a value (debug, `write out %!app%`)? With module-style output it's its `[Out]` face.

## Tests

- `%!app.goal["/start"]%`, `%!app.setting.id%` and `%!app.test.setting.parallel%` still resolve.
- An app-start before/after binding fires around the entry goal, a cancel skips the goal, and it doesn't fire in Build mode.
- `Clone` answers the same app.
- The type list holds `app`.
- `%!app.variable%` answers the concept.

## Comparison with the coder's trace

(to fill in after reading it)
