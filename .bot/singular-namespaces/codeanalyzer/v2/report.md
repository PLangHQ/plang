# Code Analyzer — v2 — `singular-namespaces` (re-review of coder fix + fresh eye)

**Reviewing:** `6b4c3e5e0..HEAD` (2 commits):
```
3a4f9a616 codeanalyzer v1: door symmetry + dead-enumerator cleanup   ← the fix for v1
e7a968c0f generator: emit ? on nullable Data<T> partials to match hand-written declarations
```
**Build:** `dotnet build PlangConsole` + `dotnet build PLang.Tests` → 0 errors (clean rebuild).
**Suite:** 3694 tests, 3693 pass. The 1 counted failure is **`AppType_IndexByName_Fields_OnRecordType_FoldedFromEntry`** — the very test that is supposed to prove v1 finding #1 is fixed. (A second name, `BuilderValidate_CallsBuildOnEachAction_InOrder`, appears in full-run output but passes in isolation — a pre-existing parallel-ordering flake, unrelated.)

---

## Verdict: **FAIL**

The fix did not close finding #1 — it **traded a silent null for a non-deterministic null**, and its own proof-test fails on a clean build. The generator change and dead-enumerator work are fine; one dead enumerator remains.

---

## Finding 1 (re-review) — the door fix is unsound: fold properties are now NON-DETERMINISTIC

### What the coder did
`app.Type[name]`/`of<T>()` now return the **catalog-built** entity from a new memoized cache instead of `new @this(name)`, and `Promote()` reads that same cache:
```csharp
// type/list/this.cs:171
_catalogByName = new Lazy<Dictionary<string, app.type.@this>>(() => {
    var dict = new Dictionary<string, app.type.@this>(StringComparer.OrdinalIgnoreCase);
    foreach (var entry in BuildTypeEntries(null))   // seed = KnownTypes()
        dict.TryAdd(entry.Value, entry);            // ← FIRST-WINS on name collision
    return dict;
});
// indexer :189   if (CatalogByName.TryGetValue(typeName, out var built)) return built;
// type/this.cs:137  Promote(): if (!Context.App.Type.ComplexSchemas().TryGetValue(Value, out var match)) return this;
```
The intent is right (hand back a populated entity so fold props don't need a Context stamp), and the v1 test stamps were correctly removed.

### Why it's broken
The cache is populated **first-wins (`TryAdd`) over an unordered type set**:
- seed is `BuildTypeEntries(null)` → `KnownTypes()` = `_typeToName.Keys.Concat(_runtimeNameToType.Values).Distinct()` (`Registry.cs:96`) — a `Distinct()` over dictionary keys, **no defined order**.
- those keys come from `SafeGetTypes(asm) => assembly.GetTypes()` (`Registry.cs:196`) — reflection order is **not contractually stable**.

When **two CLR types map to the same PLang name**, whichever `BuildTypeEntries` emits first wins the `TryAdd`. If the winner is a barren entry (no `Fields`) it **shadows** the populated one. `"goal"` collides this way: `app.Type["goal"]` resolves to an entry whose `Fields` is null depending on enumeration order.

### Reproduction (clean build, stale-binary trap honored)
```
# in isolation, 8 consecutive runs:
AppType_IndexByName_Fields_OnRecordType_FoldedFromEntry → PASS=0 FAIL=8

# co-executed with its two sibling FoldedFromEntry tests:
run 1 → 1 failed / 3      run 2 → 0 failed / 3
```
Pass/fail flips with **what else runs in the process** (which perturbs reflection/assembly-load order). The contract `app.Type["goal"].Fields != null` is non-deterministic. The coder's commit looked green because it was verified under a co-execution order that happened to let the populated `"goal"` entry win — or against a stale binary (the trap CLAUDE.md warns about). On a clean build in isolation it fails every time.

### Both doors are affected
`Promote()` (the `data.Type` door) now reads the **same** cache via `ComplexSchemas()` (`type/this.cs:137`; `ComplexSchemas() => CatalogByName`, `this.cs:627`). So `data.Type` of a `"goal"`-typed value inherits the same non-deterministic `Fields`. The pre-fix `Promote()` called the fresh `BuildTypeEntries(Context.App.Module)` walk — different (broader) coverage. The fix narrowed `data.Type`'s fold source to the collision-prone null-catalog cache as a side effect.

### Root-cause smell (Pass 4.5)
This is tell #8 (mirror/re-derive) + a determinism defect: a cache keyed by a name that is **not unique over the seed set**, resolved by arbitrary first-wins. `ResolveName` already documents a "first-wins rule" (`this.cs:621`) — fine for name→type where any mapping resolves, but **wrong** for name→entity where one candidate carries fold data and another doesn't. First-wins silently picks the barren one some fraction of the time.

### Root-level fix
Make collision resolution deterministic **and** shape-preferring — on duplicate name, keep the entry whose `Fields`/`Values`/`Shape` is non-null (never let a barren entry shadow a populated one); and/or order the `KnownTypes()` seed stably (e.g. by `FullName`) before the walk. Then re-run `AppType_IndexByName_Fields_OnRecordType_FoldedFromEntry` **in isolation** (not just in a full suite) — that ordering is where it fails. Confirm the `data.Type` path under the same `"goal"` collision while you're there.

---

## Finding 3 (re-review) — one dead enumerator still dead, now with a misleading comment

- `channel/list.All` — **deleted** ✓.
- `goal/list.Value` — **deleted** ✓ (this was the one v1 named).
- `goal/list.All` — **kept**, with a new comment: *"`All` stays as a non-IEnumerable alias for compatibility callers."* It **is** `IEnumerable<goal.@this>` (`goal/list/this.cs:299`), and it has **zero callers** in source or tests (`grep .Goal.All` / `Goals.All` → empty). So a dead enumerator survives behind an inaccurate justification. v1's ask was "one of each pair should go"; for goal the canonical `list` plus dead `All` is the same redundancy with the names swapped. Low severity — delete `All` or correct the comment, but don't leave a dead member defended by a wrong comment.

---

## What's clean

- **Promote() perf (v1 finding #2):** now hits the memoized `ComplexSchemas()` instead of re-walking `BuildTypeEntries` per entity. The intent is right — but note the memoization is the same mechanism that introduced Finding 1's non-determinism; fixing the collision resolution fixes both.
- **Primitive door:** `app.Type["int"]` returns `new @this(typeName, Get(typeName))` with `ClrType` pre-stamped from the static path — correct; primitives have no fold data, so no Promote rebuild is the right answer. The `ClrType` test passes deterministically.
- **Scheme residual:** the one surviving `t.Context = app.User.Context` stamp (TypeAccessorTests:56) is for `Scheme`, honestly commented as Context-dependent. Acceptable and not masking the Finding-1 gap (the indexer doc comment is correctly scoped to "catalog fold data + ClrType", not Scheme/Kind).
- **Generator change (`e7a968c0f`):** emitting `{TypeName}?` on the public partial when `IsNullable`, while keeping the backing field non-`?` so `init` can set it, correctly matches the hand-written partial declaration — a real nullability-annotation fix, minimal and well-scoped. Clean.

---

## Bottom line
The reshape itself remains sound (v1's clean items hold). The single blocker is that the fix for finding #1 is **non-deterministic**: `app.Type[name].Fields` (and `data.Type` for the same value) is null on a fraction of clean-build runs because the entity cache is first-wins over an unordered type set with name collisions. Its proof-test fails 8/8 in isolation. Deterministic, shape-preferring collision resolution closes it; re-verify in isolation, not just in a full-suite run.
