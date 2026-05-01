# Coder v1 — Data Identity Preservation + [VariableName] Migration

## Inputs

- Architect plan: `.bot/runtime2-data-share-state/architect/v1/plan.md` (6 phases)
- Test-designer contract: `.bot/runtime2-data-share-state/test-designer/v1/plan.md` (62 C# tests across 9 files + 4 PLang `.test.goal` files, all with `Assert.Fail("Not implemented")` stubs)
- Baseline: 145/145 plang tests green (64 failing/stale `.goal` files renamed to `.goal2` to be brought back online incrementally as features land).

## Anchor (architect's principle)

Every plang variable IS `Data`. Cross-type views are LIVE windows into the same variable — `Properties` and the three event lists share by reference; only `Type` and the converted `.Value` differ. `variable.set` is the **sole** binding-mint site.

Three property shapes:
- **plain `Data`** — variable references; handler operates on the live variable.
- **`Data<string>`** — literal name slots; `.Value` IS the bare name.
- **`Data<T>`** — typed value slots; standard wrapper.

## What to build, in order

I'll land the architect's 6 phases in a slightly tighter ordering, driven by what unblocks the most tests. Phases 1+2 must land together (the contract that 60% of the new tests check). After that, Phase 3 → 4 → 5 → 6 in order, each with green tests before moving on.

### Phase 1 + 2 (single coherent commit) — Data foundation

**Files touched:**
- `PLang/App/Data/this.cs` — events from `event Action<...>` to `List<Action<...>>` (Phase 1); `As<T>` / `ConvertAndWrap` rewrite for identity preservation (Phase 2b); name propagation (Phase 2a); `IsPlangIterable` / `IsPlangAssignable` predicates (Phase 2c); `As<IEnumerable>` delegates to `AsEnumerable` (Phase 2d).
- `PLang/App/Debug/this.cs` — change `+=` to `.Add(...)` at the four event-subscribe lines.
- `PLang/App/Variables/this.cs:76` — `dv.CopyEventsFrom(prev)` becomes alias-by-assignment (anticipates Phase 3 but the immediate need is build-correctness once `CopyEventsFrom` is gone).

**Implementation moves:**

1. **Events → Lists.** Replace the three `event Action<...>?` declarations (`Data/this.cs:96-102`) with `List<Action<...>> { get; set; } = new();`. `FireOnChange/Create/Delete` become `foreach (var h in OnChange) h.Invoke(...)`. Delete `CopyEventsFrom` (`:105-110`).

2. **Constructor lazy-init.** Make `Properties` and the three event lists null-coalesce in the constructor body so initializer-syntax `{ Properties = source.Properties, OnCreate = source.OnCreate, ... }` wins. Pattern: `Properties = Properties ?? new Properties();` etc. (or use `??=`).

3. **Plang-assignability predicates.** Add private `IsPlangIterable(object?)` and `IsPlangAssignable(Type, Type)` static helpers. Point existing carve-outs in `AsEnumerable()` (`:311`) and `EnumerateItems()` (`:345`) at `IsPlangIterable`. One source of truth.

4. **Name propagation in `AsT_Impl`.** At the full-match arm (`:438-447`), the recursion currently re-enters `AsT_Impl<T>` on `this`, losing the live variable's identity. Refactor: when full-match resolves to a live variable, the live variable's Data becomes the canonical for the wrap. Rule:
   - Full match (`%var%` IS the whole value) → canonical is `resolved` (live var). `Name` propagates. Value is `resolved.Value`.
   - Partial match (`"hello %x%!"`) → canonical is `this`. Name stays the slot's.
   - Unset `%var%` → still propagate the name (per test contract `Name_UnsetVariable_PropagatesVarName_NotInitialized`); construct a not-initialized Data with that name.
   - Literal value (no `%`) → canonical is `this`.

5. **Identity-preserving wrap.** Replace `ConvertAndWrap` with a new helper that takes an explicit `canonical` Data and routes through the four cases:
   ```csharp
   private @this<T> WrapAs<T>(@this canonical, Actor.Context.@this? ctx) {
       // Rule 1 — same-type fast path
       if (canonical is @this<T> sameTyped) return sameTyped;

       // Rule 2 — variance fast path (U:T where U is Plang-assignable to T)
       if (canonical.Value is T castOnly && IsPlangAssignableFrom<T>(canonical)) {
           var w = new @this<T>(canonical.Name, castOnly, canonical.Type, canonical.Parent) { Context = ctx };
           AliasState(w, canonical);   // Properties + 3 event lists by ref
           return w;
       }

       // Rule 3 — cross-type with conversion
       object? converted;
       ServiceError? err;
       if (typeof(IEnumerable).IsAssignableFrom(typeof(T))) {
           converted = canonical.AsEnumerable();   // Phase 2d — Data owns it
           err = null;
       } else {
           (converted, err) = TypeMapping.TryConvertTo(canonical.Value, typeof(T), ctx);
       }
       if (err != null) return @this<T>.FromError(err);
       var wrapped = new @this<T>(canonical.Name, (T?)converted, canonical.Type, canonical.Parent) { Context = ctx };
       AliasState(wrapped, canonical);
       return wrapped;
   }

   private static void AliasState(@this dst, @this src) {
       dst.Properties = src.Properties;
       dst.OnCreate   = src.OnCreate;
       dst.OnChange   = src.OnChange;
       dst.OnDelete   = src.OnDelete;
   }
   ```

6. **Plain `Data` target — bypass `As<T>` wrapping.** The Data emission path (`PLang.Generators/Emission/Property/Data/this.cs:35`) currently uses `As<object>(Context)` for plain Data. Per architect Phase 2 Rule 4 and tests `AsT_PlainDataTarget_LiteralParameter_*` / `AsT_PlainDataTarget_VarReference_*`, plain Data should return the **canonical** as-is — no wrap.

   Two options:
   - **(a)** Add a method `Data.@this AsCanonical(Context)` that returns the live variable Data on full-match, parameter Data otherwise. Generator emits `__ResolveData(...).AsCanonical(Context)` for plain `Data` properties.
   - **(b)** Make `As<object>()` itself short-circuit to canonical when there's no conversion needed. Risky — `As<object>()` is also used externally and must keep returning `Data<object>`, not arbitrary `Data<T>`.

   Going with **(a)** — it's a clear, named OBP method, and it changes only the generator emission for plain Data, leaving `As<T>` semantics unchanged for typed slots.

7. **`As<IEnumerable>` delegates to `AsEnumerable` (Phase 2d).** In Rule 3 cross-type, when `T` is or contains `IEnumerable`, call `canonical.AsEnumerable()` instead of `TypeMapping.TryConvertTo`. Single source of truth for enumeration semantics.

**Risks for this phase:**
- Existing code paths that use the events as C# delegates (`OnChange?.Invoke(...)` syntax, `+=` syntax). Audit before commit: `grep -rn "OnChange\|OnCreate\|OnDelete" PLang/` and update each.
- `Properties = canonical.Properties` aliasing means **two** Data instances share Properties. If anything later assumes Properties is per-instance, it breaks. The architect specifically calls this out as the desired behavior.

### Phase 3 — Variables.Set dumb storage (single small commit)

**Files touched:**
- `PLang/App/Variables/this.cs:38-101` — strip the wrap-and-fire logic on the Data path; alias prev's three event lists onto `dv` on every replacement.

**Open question (architect/v1/plan.md:284,290):** does `Properties` also alias from prev to dv on replacement?
- Architect's stated default: **no** — Properties is per-binding metadata.
- Test-designer pinned this default in `Set_PropertiesNotAliased_NewBindingHasOwnProperties`.
- I'll honor the stated default. **If Ingi wants Properties to also alias, flip the test and the alias call in the same commit.**

**Implementation:** straightforward per architect's pseudo-code (`plan.md:108-124`). The non-Data wrap overload (`Set(string, object?, Type?)`) — test-designer summary says this gets dropped entirely. I'll drop the overload and update callers (which today rely on the overload to wrap raw values; after Phase 4 they don't, since `variable.set` mints Data itself). Audit callers via grep; the only legitimate ones today are `variable.set` (Phase 4 takes ownership) and dot-path navigation (which keeps its own helper).

### Phase 4 — variable.set always-types (single commit)

**File touched:**
- `PLang/App/modules/variable/set.cs` — rewrite per architect Phase 4 (if-chain + reflection fallback).

**Implementation:** type inference via `value.Value switch { string s => new Data<string>(name, s), int i => new Data<int>(name, i), ... }`. Hot types in the switch; uncommon types fall through to reflection (`typeof(Data.@this<>).MakeGenericType(value.GetType())`). Mutable refs (List, Dict) get JSON-roundtrip snapshot clone — matches the existing pattern at `list/add.cs:67-69` and the Variables.Set dot-path at `Variables/this.cs:147`.

`AsDefault` semantics preserved: check `Variables.Get(Variable.Value).IsInitialized` before constructing.

**One coordination point:** the `[VariableName] partial string Name { get; init; }` declaration on `Set` has to stay until Phase 5/6 lands the `Data<string> Variable` shape. I'll migrate it as part of Phase 5 (variable handlers) — keeping the current property declaration during Phase 4 minimizes blast radius. The test-designer's `SetTypeInferenceTests` exercises through the runtime, not the declaration shape.

### Phase 5 — Handler migration (multiple commits, one per module folder)

**Files touched:** ~25 handlers across `PLang/App/modules/{variable,list,loop}/`.

**Pattern A — plain `Data` (mutate-in-place):** `list.add`, `list.remove`, `list.set`, `list.reverse`, `list.sort`. The handler reads `List.Value as List<object?>` and mutates; no Variables.Set write-back. Returns the live variable's Data.

**Pattern B — `Data<string>` (literal name slot):** `variable.set.Variable` (rename `Name` → `Variable` per architect's example syntax), `foreach.ItemName`, `foreach.KeyName`, `variable.exists.Variable`, `variable.remove.Variable`, `variable.get.Variable`, `variable.clear.Variable` (if it has one). `[VariableName]` attribute usage on these properties is replaced by `Data<string>` — the slot value IS the bare name.

**Pattern C — `Data<T>` (standard typed):** `list.set.Index` (`Data<int>`), `variable.set.Type` (`Data<string>?`, already this shape), `loop.foreach.Collection` becomes `Data<IEnumerable> Collection` per architect's classification.

**Read-only collection consumers** (architect plan §Phase 5 typed-collection migration): `list.any/contains/count/first/last/indexof/join/flatten/group/range/unique/split` and `loop.foreach.Collection` move to `Data<IEnumerable> Collection`. Variance Rule 2 keeps the underlying ref shared for `List<object?>` sources — no functional change for those handlers.

**Builder-prompt change:** the `[Example("...", "...")]` attributes for variable.set / list-handlers update to use bare names (no `%`) for Pattern B slots. Pattern: `Variable([string] data)` not `Variable([string] %data%)`.

**Order within Phase 5:**
1. `loop/foreach.cs` first — its Pattern B for `ItemName/KeyName` is the simplest and exercises the new `Data<string>` shape.
2. `variable/set.cs` second — coordinated with Phase 4 (already moved off raw scalars there for `Value`; this phase moves `Name` → `Variable: Data<string>`).
3. `variable/{exists,get,remove,clear}.cs` — small, mechanical.
4. `list/*.cs` — alphabetical, each independent. Mutate-in-place handlers (add/remove/set/sort/reverse) drop the `Variables.Set(name, list)` write-back since the live ref handles propagation.

**Each handler gets a smoke test before moving to the next.** The `dotnet run --project PLang.Tests` suite (TUnit) catches the C#-side regressions; the rename'd `.goal2` files come back online as `.goal` once the relevant handler is migrated.

### Phase 6 — Generator + attribute deletion (final commit)

**Files touched:**
- **Delete** `PLang.Generators/Emission/Property/Legacy/this.cs`.
- **Delete** `[VariableName]` attribute (`PLang/App/Attributes/...`).
- **Update** `PLang.Generators/Discovery/this.cs` — PLNG001 allows only `Data<T>`, plain `Data`, `[Provider] T`. Remove `[VariableName]`.
- **Delete** `__StripPercent`, `__Resolve<T>`, `__HasParam`, `RawScalarValidations` from `PLang.Generators/Emission/Action/this.cs:250-298`.
- **Delete** the pre-Run resolution check at `PLang.Generators/Emission/Action/this.cs:232` (post-Run check from coder/v6 stays).

After this, the Legacy emission path is gone. Any property declaration that doesn't fit one of the three approved shapes fails the build with PLNG001 — backstop against future regression.

## Test strategy

Per Ingi's instruction: **C# tests AND PLang tests are required.** Test-designer wrote 62 C# tests + 4 PLang tests as `Assert.Fail("Not implemented")` stubs — those are the spec. I'll fill them in as each phase lands, alongside any new tests needed for the migration.

For each phase:
1. Implement the C# changes.
2. Build (`dotnet build`).
3. Fill in the relevant test bodies (test-designer's stubs become real assertions).
4. Run C# tests (`dotnet run --project PLang.Tests`).
5. Run plang tests (`plang --test` from `Tests/`).
6. Bring `.goal2` files back to `.goal` as features land that unblock them — verify each is green before continuing.

Existing tests that change shape during Phase 5 (e.g. `PLang.Tests/App/Modules/loop/ForeachTests.cs` for `ItemName`/`KeyName` becoming `Data<string>`) get migrated in the same commit as the handler.

## Going-back-online plan for the `.goal2` files

The 64 renamed tests fall into roughly six groups by likely root cause. As each phase lands, the matching group can come back online:

| Group | Approx files | Likely cleared by |
|------|---|---|
| `Modules/Test/*` (10) | TestSystemTestGoal*, TestReport* | Phase 1+2 (cyclic-resolution / sensitive-masking surfacing changes — likely already cleared, just need a re-run) |
| `Modules/Signing/*` (10) | Signing handlers | Phase 5 handler migration (typed collections, identity-preserving wraps for sign/verify) |
| `Modules/Event/*` (6) | Event before-step / multiple / wildcard | Phase 1 (event lists) |
| `Modules/Variable/*`, `Modules/Loop/*` (rest) | Variable + Loop handler tests | Phase 4 + Phase 5 |
| `Modules/List/Mutation/ListAddVisibleAfterCall.test.goal2` | new test from test-designer, stub body | Phase 5 (`list.add` Pattern A) |
| Misc (Identity, Cache, Crypto, etc.) | ~15 tests | re-run after each phase; many will clear as a side effect of the foundation changes |

I won't try to bring them all back at once — each phase ends with "bring back online whichever tests that phase actually fixes," with verification.

## Commits — proposed sequence

1. **Phase 1+2 foundation** (single commit, ~6 files): events→Lists, As<T> rewrite, IsPlangIterable predicate, name propagation, plain-Data canonical bypass. Plus the C# tests in `EventListTests.cs`, `AsTIdentityTests.cs`, `NamePropagationTests.cs`, `PlangAssignabilityTests.cs`. Stable point — all tests green.
2. **Phase 3 Variables.Set dumb storage** (1 file): rewrite `Set`. Plus `SubscriberSurvivalTests.cs`. Bring back online the `.goal2` tests that Phase 1+2 already fixed.
3. **Phase 4 variable.set always-types** (1 file): rewrite `Run`. Plus `SetTypeInferenceTests.cs`. Plus the `TypeInference.test.goal` (rename .goal2 → .goal, fill in real body).
4. **Phase 5a foreach migration** (1 file + tests): `loop/foreach.cs` to Data<string> shape. Plus `ForeachStringNotIterableTests.cs`. Bring back `ForeachStringNotIterable.test.goal`.
5. **Phase 5b variable handlers migration** (5 files): `variable/{set,exists,get,remove,clear}.cs`. No new tests (existing migrated). Bring back any cleared `.goal2`.
6. **Phase 5c list handlers migration** (~18 files): `list/*.cs`. Plus `ListAddIdentityTests.cs`. Plus `ListAddVisibleAfterCall.test.goal` and `SetSnapshotClone.test.goal`.
7. **Phase 6 generator + attribute cleanup** (final): delete Legacy emission, `[VariableName]`, raw-scalar generator code. Plus `Plng001PostMigrationTests.cs`. Bring back any remaining `.goal2`.

Each commit ends with green C# + green plang test runs. Single PR but coherent commit history — easier to bisect if a regression slips in.

## Decisions from Ingi (2026-04-30)

1. **Clone semantics for `variable.set` with Data source.** `set %x% = %y%` produces a clone of `%y%` named `x` — Properties cloned, event lists cloned (shallow — same delegates, separate list instances), Value cloned. No reference-aliasing between %x% and %y% afterward. **Variables.Set is dumb storage** — no special prev→dv event merging on replacement. The architect's "alias prev events onto dv" idea is dropped. Subscribers attached to a specific Data instance do not survive its replacement (debug-watcher survival, if needed later, lives on the Variables collection — out of scope here).

2. **Keep `Name` on `variable.set`.** Do NOT rename the slot to `Variable`. Existing `.pr` files keep working. Pattern B (Data<string> for literal name slot) still applies — the property declaration becomes `public partial Data.@this<string> Name { get; init; }` instead of `[VariableName] partial string Name`.

3. **Bring back online**, in scope to fix as the work progresses:
   - `Modules/Variable/*` (3 tests)
   - `Modules/Loop/*` (2 tests)
   - `Modules/Test/*` and the `TestModule/*` duplicates (~10 + duplicates)
   - `Modules/List/Mutation/ListAddVisibleAfterCall.test.goal` (new from test-designer, body to fill)

   **Stay sidelined as `.goal2`** (Ingi confirmed these aren't worth fixing in this branch):
   - `Modules/Signing/*` (10)
   - `Modules/Event/*` (6)
   - `Modules/Identity/*` and `Identity/*` (5)
   - `Modules/Crypto/HashBcryptVerify` (bcrypt issue, fine)
   - `Modules/Cache/*`, `Modules/Condition/Compound/Mixed/`, `Modules/Condition/Files/*`, `Modules/Error/*`, `Modules/Goal/*`, `Modules/Http/DownloadFile`, `Modules/Ui/*`, `Modules/Builder/ValidateValid`, `App/SetupGoal/`, `Builder/ForeachCallsGoalPerItem` — not asked for.

4. **No PR.** Commit when done; next bot picks up. So I'll commit per-phase as I go (clean history) and finish with a wrap-up summary commit.

## What I'm NOT doing (explicit non-scope)

- `event.on` plang-action for Data CRUD (architect explicitly defers to a follow-up branch).
- Refactoring `Variables.Set` dot-path navigation (`Variables/this.cs:104+`) — that's its own concern.
- Restructuring `TypeMapping` — Phase 2d delegates to `AsEnumerable`, but `TypeMapping.TryConvertTo` keeps its current shape for non-IEnumerable conversions.

---

**Ready for your approval before I start coding.**
