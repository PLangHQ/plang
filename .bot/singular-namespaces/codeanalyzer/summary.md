# Code Analyzer — summary (singular-namespaces)

**Version:** v1

## What this is
Review of the `singular-namespaces` refactor: `PLang/app/**` folders/namespaces moved from plural to singular ("an entity's home is singular; a collection over it is a child"), each concept gaining an `X/list/this.cs` registry reached via a singular `app.X` accessor, and the Stage-4 fold of the old `builder.type.Entry` parallel struct into the `app.type.@this` entity.

## What was done (this review)
- Pulled the branch, reconciled the coder v1 report against HEAD — **the report is stale**: four commits land after it. The Entry/EntryKind structs are genuinely gone, plural `App.Goals`-style properties are gone, `Variable` is now `@this`. Analyzed what actually shipped at `f7790b3a6`.
- Clean build verified: 0 errors, 510 pre-existing nullability warnings.
- Surveyed all 16 list-registries + `module/this.cs` + `type/this.cs`.

### Verdict: **NEEDS WORK** (FAIL)

**Clean:** Entry struct dissolved (central OBP smell closed), no type-switching in any registry (architect's main risk resolved), index-miss hard-throw consistent across selection registries, build clean.

**Blocker (one finding, undisclosed — not on coder's deferral list):**
`app.Type[name]` and `of<T>()` return `new app.type.@this(typeName)` with **no Context** (`type/list/this.cs:161,172`). All catalog properties (`Fields`/`Description`/`Example`/`Scheme`/`Kind`/…) route through `Context` and are therefore silently null off that door, and `ClrType` falls to the static map (null for DLL/user types). The `data.Type` door *does* stamp Context (`data/this.cs:81`) — so the two doors diverge, despite the `type/this.cs:13` doc comment promising they're equivalent. `TypeAccessorTests` only passes because every fold case manually does `t.Context = app.User.Context` — **the test asserts the workaround**, not the contract. Root: catalog/fold data is App-global but reached through an actor `Context`; the registry has no Context to stamp.

**Low:** `Promote()` re-walks the whole catalog + linear self-scan per stamped entity (`BuildTypeEntries` not memoized). Two dead enumerators: goal/list `Value` (beside new `list`), channel/list `All` (beside new `list`).

## Fix direction for the coder
Make the indexer return the catalog-built entity (cached), or back the type entity by `App` instead of actor-`Context` so global type metadata resolves without a stamp — then the doc comment is true and the six `.Context =` lines in the test delete. Drop the two dead enumerators.

## Next
`run.ps1 coder singular-namespaces "Fix the type-entity door asymmetry (app.Type[name]/of<T>() return contextless entities with silently-null catalog props; data.Type door stamps Context, indexer does not; test asserts the manual-stamp workaround) and drop dead enumerators goal/list.Value and channel/list.All" -b singular-namespaces`
