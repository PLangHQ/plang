# fast-apps: OBP review of the whole production diff (decision 61)

Scope: `git diff 98624ab6a..4d28eda40 -- PLang PLang.Generators` (69 files, +1760/-653; PLang.Generators unchanged). Read only. I did not build, run tests, or run `Tools/ObpScan`. Three region readers read the touched files whole and walked callers; I re-checked the blocking findings and the ones marked (verified) myself with grep. Findings marked (reader) were not re-grepped; the shape holds, a line may be a few off. Deferred items (34's S1, 35's Registry statics and public `Assemblies`, 42's 12–14, 41's identity provider, the code providers not layered) are not re-raised unless worse.

**Verdict: NEEDS WORK (FAIL, coder next).** The architecture holds: every app made from an origin with two kinds, lists as layers, the hold, the role type, the history variant, the lazy tables published safely. No `caller == null`, no `_parent` null, no `Shared`, no static added, no run-time binding on a shared object, no broken seal, no cross-file lock. **Three findings are blocking** because they break the branch's own rules (35, 33, 40); the rest should be fixed but need not hold the merge.

## Blocking

### 1. Production code only tests call (rule 35). `store/sqlite/this.cs:54` (verified)
`public static @this InMemory(actor.context.@this context) => new(null, () => true, context)`. Production makes its store through `IStore.Open`; the only callers are `PLang.Tests/Runtime/App/Settings/InMemoryStoreTests.cs` and `PLang.Tests/Modules/App/Modules/datasource/DataSourceTests.cs`. Decision 57 reworked it (dropped its name) to keep it for tests.
- **Fix shape:** delete it. Tests construct the store the way production does (`Open` through the provider) or call the constructor the test already reaches.
- **Blocking:** yes (35).

### 2. A flag checked at many doors. `store/sqlite/this.cs:65`, `:123`, `:148`, `:238`, `:262` (verified)
`private bool IsBlank => Volatile.Read(ref _opened) == null && _memory();` is tested at the head of four verbs (`Get`, `GetAll`, `Exists`, `Tables`), each building its own empty answer (`Context.Ok<T>(default!)`, `Ok(false)`, and twice `new list.@this(Context.App.type.list["list"])`, a by-name lookup beside the natural call). This is decision 33's "flag checked at many doors" in a new place.
- **Fix shape:** the store answers once. One member hands back the connection or the typed empty answer for the verb (a blank in-memory store is a variant that answers empty, not an `if` in each verb); the empty list is made in that one place.
- **Blocking:** yes (33).

### 3. "Is this layer's own?" asked by reference at the module registry and the type list (fork). `module/this.cs:90`, `:126`, `:163`; `type/list/Registry.cs:112` (verified); `type/list/this.cs:203`, `:322` (reader)
- `module/this.cs:126` `if (!ReferenceEquals(owner.Root, Root)) App.origin.Register(owner, actionName, type); else owner.Add(actionName, type);`
- `module/this.cs:163` `ReferenceEquals(context.App.module, Root) ? Drop(context) : context.App.origin.Remove(this, context)`
- `module/this.cs:90` `HasOwn => list.Items().Any(module => ReferenceEquals(module.Root, this))` (a scan of every module, read through `app.HasCode` on every goal take, `app/this.cs:60`, `actor/this.cs:187`), beside the type list's O(1) `Registry.cs:109 HasOwn => !_named.IsEmpty`.
- `Registry.cs:112 Holds(entry) => ReferenceEquals(_named.GetValueOrDefault(entry.Name), entry)`, read at `Registry.cs:264` and `list/this.cs:203`.
Decision 40 ruled "not an `IsOwn` bool read in an `if`", and decision 22 said `Full` and `Member` go to the list that made the entry. This is that bool spelled as `ReferenceEquals`, with two answers to one fact ("has code of its own") from two owners.
- **Fix shape:** the element already knows its list: a module holds `Root`, a type holds `Types`. The registry that holds the module answers `Take(module, action, type)` and `Drop(module, context)`; the type's own list answers `Full`/`Member`; only the bottom (the code, which refuses) keeps a branch. "Has code of its own" is one count the layer updates (the registry's non-chunk rows), read by both, so no scan and no `ReferenceEquals` per goal load.
- **Blocking:** yes (33, 40).

## Should fix, not blocking

### 4. Dead code left by the hold (deletion test). `event/this.cs:30` and nine overrides (reader; I found no caller)
`protected virtual @this? Of(event.on.@this on) => on[Name];` with overrides `event/on/{error,load,ask,create,read,start,set,write,remove}.cs`. The only `Of(` the walks call is `event.on.list.Of(item)`/`on.Of(_level)` (`event/this.cs:132,164`), a different member. Delete the virtual and the nine overrides.

### 5. `goal.list`'s choreography sits in a static on `goal`; the index can drift from the rows (stray helper + stored twice). `goal/this.cs:359-372`, `goal/list/this.cs:77-100` (reader)
`public static Load(path source, app app)` chooses held → the origin's `Take` → `Reading(Read)` by calling three list members from outside; `Read` is static and stamps another instance's `_read`. `_held` (`.pr` → goal) is kept beside the rows by `Add` only; the inherited `Remove`, `RemoveAt`, `Clear`, `Set` and base `Add` leave it stale, so `this[pr]` and `Held` could answer a goal the rows no longer hold (no caller removes today).
- **Fix shape:** `goal.list.Load(source)` owns the three-way choice; the rows are the index (a list keyed by `.pr`) or the removals are overridden. The static `Load` predates the branch but the branch widened it (rule: no static in production).

### 6. A discipline kept by convention at five call sites (raw hand-off). `test/this.cs:180`, `test/Coverage.cs:46`, `module/on/event.cs:73`, `module/debug/this.cs:75,123` (reader)
Decision 24a: a run-time binding is held on the actor's root context. Each caller spells the route (`context.Actor!.Context.on.Bind`, `…actor.list.User.Context.on`); a caller who writes `context.on` holds it on a frame and nothing says so. **Fix shape:** the actor answers its hold (`actor.on`), so no caller picks the context.

### 7. The "first made is kept" publish is copied eleven times (stray helper). 
`x ?? Interlocked.CompareExchange(ref x, new(), null) ?? x` at `actor/this.cs:72,93`, `data/attachment.cs:13,24`, `data/this.cs:237`, `variable/list/this.cs:25`, `event/on/list/this.cs:26`, `call/list/this.cs:98`, `type/item/this.cs:512` and others (11 `Interlocked.CompareExchange` in `app/`); neighbours disagree (`actor/this.cs` `Setting => _setting ??= …` is unsynchronised; `Code.cs` `Class.Setting` uses a `Volatile` pair, `Path`/`Defaults` use `??=`; `kind/empty` publishes `_kinds`/`_tables` whole but `_format` is a plain field read without the gate). **Fix shape:** one owner for "made when first asked" (`Lazy<T>` with PublicationOnly or one holder type).

### 8. Probe before read on the attachment (fork at call sites). `wire/kind/plang/writer.cs:60,75`, `variable/code/Property.cs:37`, `module/debug/this.cs:382`, `module/variable/set.cs:334` (reader)
`Data.Attachment`/`.Kept`/`.Properties` make their bag on read, so each reader must ask `HasKept`/`HasProperties` first; a reader that forgets silently allocates. Seven copy sites also read `Attachment` (`data/this.cs:159,634,665,754,771,949,1023`) and so make the source's bag just to copy it. **Fix shape:** the reader's door answers empty without making; copies use `Carried`.

### 9. Fork by literal name and names stored twice. `type/item/variable/list/system.cs` (reader)
The indexer is a seven-arm `Is(name, "Now") ? … : Is(name, "NowUtc") ? …` chain; `All` repeats the same seven names in a literal array in another order; `Contains` makes the variable to answer `!= null`. **Fix shape:** one table, name → maker, read by the indexer and `All`.

### 10. Throwaway object made to read one member. `type/list/Code.cs:262` (reader)
`Kind = ((ITypeReader)made.Invoke(null)).Kind;` builds a reader only to ask its kind, then `IndexAssembly` builds a second per list. **Fix shape:** keep the first as the code's reader if readers hold no per-app state; else the reader class declares its kind as a fact, as `Class` does for `Alias`.

### 11. The origin refuses in two voices; `Hold` answers null for "held". `origin/this.cs:36`, `origin/code.cs:36-40`, `actor/this.cs:197,201` (reader)
`Hold` returns `error?` (null = held); `Register` and `Remove` throw `AppException`, though `Remove` returns `ValueTask<bool>`. **Fix shape:** one answer shape (`Data` Ok/Fail for `Hold` and `Remove`; only the void `Register` throws).

### 12. A bool flag threaded through births, default-true (flag false by default). `app/this.cs:296,372`, `actor/this.cs:123`, `channel/list/this.cs:103` (reader)
`autoWireConsoleChannels = true` → `app.Console` → `console` → `standard` → `_standard`: one bool, four names, a two-word default-true flag; `Wire(bool interactiveInput)` picks an input by a second bool. **Fix shape:** a choice (`Channels: console | none`) or the origin answers (the command line's app has the consoles, a sub app none).

### 13. The fallback written twice again (decision 57 regressed). `type/this.cs:~828`, `:843` (reader)
`types?.code[clr] ?? new list.@this.Class(clr)` in the constructor and `Family._list?.code[clr] ?? new list.@this.Class(clr)` in `Facts`; a type that is its identity alone reflects over the class on every `Facts` call. **Fix shape:** set `_list` first; the constructor calls `Facts`.

### 14. A null that means "fresh"; a cache keyed by object identity. `goal/this.cs:102-116`, `:381`, `:407` (reader)
`if (_read is not {} read) return true;` and `_read = stamp.Success ? await stamp.Value() : null`: a goal whose stat failed at read time is never read again and a goal not read from a file looks the same (the late stamp is the known S1; this sentinel is worse than written). `.pr` is cached by `ReferenceEquals(made.Source, source)` because `Path` can change three ways. **Fix shape:** the goal answers `IsFresh` from a stat it holds (or a typed "no file"); `.pr` derived where `Path` is set.

### 15. Every context makes the hold (stage 4's "costs what it uses"). `actor/context/this.cs:137` (reader)
`on = new(owner);` runs for every frame and child context while only the actor's root context is read (`event/this.cs:132,164`, `module/on/event.cs:73`). **Fix shape:** the root's hold, or the actor's (with 6).

### 16. Test runner. `test/list/this.cs:95,123,131` (reader; `found` verified)
`var found = new List<file>()` is a role word (`files`); `parallel < 1 ? ProcessorCount : parallel` makes 0 mean "all cores" (a sentinel; the setting should answer its width); the `Parallel.ToInt32()` read is repeated.

### 17. Names (Ingi's list, new in this diff)
`goal/list` `Held` (participle, role word; the Held family is already on the follow-up), `Reading` (a gerund taking a callback; `Read`), `mine`/`theirs` (`:58-59`); `goal/this.cs` `Fresh` returns `Task<bool>` (`IsFresh`), `Built`; `Take` means two things (`goal.list.Take(source, context)` and `action.Take(handler, clause)` at `goal/step/action/this.cs:266`); `module/debug/this.cs:75` `var user = …User.Context.on` and `:123` `var held` (creative names for an `event.on.list`); `channel/list` `Adopted`, `Table` (for a dictionary); `Bound` means three things (`event.on.Bound(name)`, a private `Bound(item, context, side)`, `IsBound*`); `call/list` properties `_call`, `_binding`, `_isolated` (field underscores); `node/this.cs` `_setting`; `module/code/this.cs` a nested `tables`, a property `Tables` and a local `tables`, `BuiltIn<T>(…)` a noun that registers; `Code.cs` `Token`/`Payload`/`Mark` (protocol words), `Library.Typed/Read/Marked`, `Class.Where()` (a verb returning a path, clashes with LINQ), `Declared`/`defaulted` (role words), `Registry.Taken(...)` (a participle that makes an error); `kind/empty` a nested record `Tables` with a property `Current`; `variable/list` `_variables` and `Table` (two names for one dictionary), `After` names an event as a string and as a member.

### 18. Smaller
- `type/item/list/this.cs:62-73` `_chunks` typed `@this[]?` holds a `TList[]` and `is TList[] chunks` relies on array covariance; a second `TList` would recompute and overwrite. The four `foreach (var under in Under) return …` loops run over zero or one element: name what they mean.
- `channel/list/this.cs:171-175` late stamp (`channel.Channels = this; if (channel.Actor == null) channel.Actor = Actor!`), the `Adopted` wrapper now routes the table's wiring through it; existing, low.
- `module/debug/this.cs:132-152` nine `item is goal.@this` filters after the entry is bound: the typed `Bind<T>` already on item-doors' B list; these lines are in this diff.
- `module/on/event.cs:73-74` makes an `own` in the hold before `When.Use` validates the parameters.
- `Class` ties the `System.Type` of any DLL a sub app loads into the shared code's `_class` forever: the same answer for every app (not rule 29), and the pin 19 part 3 has to release.

## Locked-core check
I found no unapproved public addition. Decisions cover: `ICache : IDisposable` (10), `IClause.Bind` (26), `history` unsealed with a virtual `Add` (30/31), the `call.list` ctor (52), the abstract `reader.@this` (41), internal `app.origin`/`app.HasCode` (36–41), `item.IsBoundBefore/IsBoundAfter/Before` (internal, 28). **Confirm:** `item.Blank()` (`private protected virtual` on item's base, `type/item/this.cs`, made in 34 to replace the `_closed` fork): 30 covers `history.Add` virtual; please confirm it covers `Blank()`. It is not a visible surface.

## Checked, clean (explicit no)
- **fork on caller/parent:** no `caller == null`, no `_parent` null, no `Shared` at any door in the touched files; the origin is asked once per part at birth (`origin/this.cs`, `origin/code.cs`, the actor as origin); the sub app's folder rule lives in the actor's `Filesystem` (`OutsideItsMaker`).
- **roles:** `actor/role/{this,system,user}.cs` is one abstract type with two kinds, no flag; permission asks `Role.Stand` once.
- **run writes a shared object (29):** the hold is cleared at dispose; the read asks each level's own events then each actor's hold, making nothing (`Bound(name)` is virtual, no `as`); `Own()` is `private protected`, its only caller `action.Take`; debug and on.event bind through `Context.on`; `history.closed` is an override, no flag.
- **lazy tables:** `kind/empty` (`_kinds`/`_tables` replaced whole), `module/code` `Tables` (volatile + gate), `variable/list` `Table` (`CompareExchange`), `call/list` `Audit`, `node/this.cs` `list` (`LazyInitializer`), `channel/list` `Table` (double-checked under `_gate`, `Volatile` publish, dispose reads without making) are published safely; `HasKept`/`HasProperties` read without making and `Copy` carries both fields.
- **`goal.list` concurrency:** `Add` under the gate replaces by reference; the index is a `ConcurrentDictionary` read without it; `Reading` keeps one read in flight per `.pr` and removes it in `finally`; the stat is taken before the read.
- **statics:** none added (`_name` in `sqlite` is an instance field); `on.Made`, `None`, `Empty`, `Emptied` are shared closed tables, the same answer for every app (29a), pre-existing.
- **removed test-only code (35):** `service.Identity`, own's `Bind` overloads, `binding.list.Add`, the reader's `ctor()`, the actor's name ctor are gone; the remaining `Bind` overloads have production callers.
- **other smells:** no cross-file lock, no envelope, no `.Clr`/broken-seal reads, no second answer in a Data's `Name`, no history or task comments in the added lines, no throwaway Data in the added lines (the throwaway is 10's reader).

## Not verified
Whether the surface golden moves for any of this (I did not run the gate); behaviour under concurrency beyond reading the publish patterns; that finding 4 has no reflection-based caller. Findings 3 and 5–16 are the readers' with spot checks only on 1, 2, 3 (the scan and the three `ReferenceEquals` lines) and the lists in 7.

## For the coder, in order
1. Delete `sqlite.InMemory`; tests use the provider's door (1).
2. One empty-answer door in the sqlite store (2).
3. The registries answer own-ness through their rows and the element's list; one count for "has code of its own" (3).
4. Delete `event.Of(on)` and its nine overrides (4).
5. Then 5–18 as the architect rules; 5, 6, 7 and 14 are the ones I'd take before the merge if the cost is small.
