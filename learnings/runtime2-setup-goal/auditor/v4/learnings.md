# Learnings — Auditor v4 (runtime2-setup-goal)

## 1. Cache vs Disk asymmetry in filtered collections

When a collection has two access paths (cache lookup and disk load), filters applied to the cache path must also be applied to the disk path. Found this in `EngineGoals`: `Get()` filters `IsSetup` but `GetAsync()` doesn't filter after loading from disk. The contract says "setup goals are only reachable through Setup.RunAsync" but the implementation only enforces this on cache hits.

**Pattern to watch:** Any time a lookup method has a fast path (cache) and a slow path (disk/network), verify both paths apply the same invariants.

## 2. Test harness masks production wiring gaps

Tests that manually construct and `Add()` objects bypass the real discovery/loading path. All 18 setup tests passed because they manually added goals to the collection, but in production, no code loads setup goals before `Setup.RunAsync` runs. The test harness created a "works in tests, fails in production" scenario.

**Pattern to watch:** When reviewing tests for a new system, ask: "Who calls the loading/discovery code in production? Is that path tested?" If tests only exercise the post-load behavior, the loading gap is invisible.

## 3. Lazy-load vs pre-load design tension

Runtime2 uses lazy loading for regular goals (`GetAsync` loads from `.pr` files on demand). But setup goals need pre-loading because they're discovered by type (`IsSetup` flag), not requested by name. This is a fundamental design tension: lazy loading works when you know the name, pre-loading is needed when you need to scan. The setup system correctly implements the iteration-over-loaded-goals pattern but nobody loads the goals first.

## 4. OBP compliance was solid

The Setup system is well-designed OBP-wise:
- `Setup.@this` owns RunAsync, IsExecuted, Record, IsTolerableError — behavior on the owner
- `Steps.RunAsync` owns iteration — smart collection (rule 5)
- Navigate-don't-pass is respected: `Record(step, engine)` passes the step object, engine for navigation
- `context.Setup` is per-request state passed as a parameter (rule 4)

Good reference implementation of OBP for future reviews.

## 5. Error tolerance patterns from runtime1

`IsTolerableError` checks for "already exists" and "duplicate column name" in error messages. This is runtime1 parity for idempotent setup (CREATE TABLE, CREATE INDEX, ALTER TABLE ADD COLUMN). The pattern is string-matching on error messages — fragile but pragmatic for SQLite error messages which are stable across versions.
