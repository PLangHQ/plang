# Code Analyzer — summary (singular-namespaces)

**Version:** v2 (re-review of coder's fix for v1 + fresh-eye pass)

## What this is
Review of the `singular-namespaces` refactor (plural→singular `PLang/app/**` namespaces, `X/list/this.cs` registries, Stage-4 type-entity Entry fold). v1 found one blocker (type-entity door asymmetry) + minor dead enumerators. The coder addressed them in commit `3a4f9a616`. This version re-reviews that fix.

## v2 result: **FAIL**

### The fix for v1 finding #1 is non-deterministic
The coder made `app.Type[name]`/`of<T>()` return a memoized catalog-built entity (so fold props no longer need a Context stamp) and rewired `Promote()` to read the same cache. Correct intent — but the cache is populated by **first-wins `dict.TryAdd(entry.Value, entry)` over an unordered type set** (`type/list/this.cs:175`; seed `KnownTypes()`→`.Distinct()` and `SafeGetTypes()`→`assembly.GetTypes()`, neither order-stable). When two CLR types map to one PLang name (`"goal"` does), a barren entry can shadow the `Fields`-bearing one depending on reflection order.

**Reproduction (clean build):** `AppType_IndexByName_Fields_OnRecordType_FoldedFromEntry` fails **8/8 in isolation**; passes sometimes when co-executed with sibling tests (process state perturbs the order). `data.Type` inherits the same flaw — `Promote()` reads the same cache via `ComplexSchemas()` (`type/this.cs:137`). The coder's "green" was a stale-binary / lucky-order artifact.

**Fix direction:** deterministic, shape-preferring collision resolution — on duplicate name keep the entry whose `Fields`/`Values`/`Shape` is non-null; order the seed stably. Re-verify the test **in isolation**, not just in a full-suite run.

### Other
- **Clean:** `channel/list.All` deleted; `goal/list.Value` deleted; Promote perf memoized; generator `?`-on-nullable-partial fix correct and minimal; primitive door (`ClrType` pre-stamp) correct; Scheme's residual Context stamp honestly documented.
- **Low residual:** `goal/list.All` was kept (zero callers) behind a misleading comment ("non-IEnumerable alias" — it is `IEnumerable`). Delete it or fix the comment.

## Next
`run.ps1 coder singular-namespaces "Fix non-deterministic type-entity fold props: the _catalogByName cache (type/list/this.cs:175) is first-wins TryAdd over an unordered type set, so name collisions (e.g. 'goal') let a barren entry shadow the Fields-bearing one — app.Type['goal'].Fields fails 8/8 in isolation. Make collision resolution deterministic and prefer the entry carrying fold data; verify AppType_IndexByName_Fields_OnRecordType_FoldedFromEntry in isolation. Also delete dead goal/list.All." -b singular-namespaces`
