# Tester v1 Summary — In-Memory SQLite DataSource

## What this is

Test quality review of coder v1's in-memory SQLite datasource implementation. The feature adds `SqliteDataSource.InMemory(name)` with a sentinel connection pattern, an `Engine.Building` object, and Actor-level routing to use in-memory DBs when Testing or Building is enabled.

## What was done

### Test suite run
- **C# tests**: 1472 total, 1472 passed, 0 failed
- **PLang tests**: None for this feature (architect explicitly scoped out — no setup.goal execution system yet)
- **Coverage**: Tool crashed (known WSL2 issue). Manual analysis performed.

### Test quality analysis

**Verdict: PASS** — no false greens found.

The 7 new tests are well-designed:

| Test | Quality | Notes |
|------|---------|-------|
| `InMemory_CrudOperations` | Good | Tests all CRUD ops against in-memory DB. Checks actual values, not just `Success`. Minor: 5 ops in one test reduces granularity. |
| `InMemory_SchemaPersistsAcrossOperations` | Good | Verifies sentinel pattern works — table created in one operation is visible in the next. Also checks Tables() list. |
| `InMemory_TwoNamesAreIsolated` | Good | Proves different names = different DBs. Checks actual values differ. |
| `InMemory_DisposeClosesDb` | Good | Proves sentinel lifecycle: create → populate → dispose → new instance starts empty. This is the hardest behavior to get right and it's properly tested. |
| `Actor_UsesInMemory_WhenTestingEnabled` | Excellent | Checks **side effect** (no `.db` dir on disk), not just return value. This is the gold standard — even if InMemory() had a bug that touched disk, this test would catch it. |
| `Actor_UsesInMemory_WhenBuildingEnabled` | Excellent | Same strong assertion pattern as above. |
| `Actor_UsesFileBacked_ByDefault` | Good | Negative test — proves the default path creates files on disk. |

### Deletion test results

Applied "if I deleted lines X-Y, would any test fail?" to each changed file:

- **SqliteDataSource.cs lines 46-58** (in-memory constructor): `InMemory_CrudOperations` and all 6 other new tests would fail. **Covered.**
- **SqliteDataSource.cs lines 64-65** (InMemory factory): All 7 new tests call this. **Covered.**
- **SqliteDataSource.cs lines 315-319** (sentinel dispose): `InMemory_DisposeClosesDb` would fail — new DB would see stale data. **Covered.**
- **Actor.cs lines 73-74** (Testing/Building check): `Actor_UsesInMemory_WhenTestingEnabled` and `Actor_UsesInMemory_WhenBuildingEnabled` would fail — `.db` dir would appear. **Covered.**
- **Engine/this.cs line 200** (Building init): `Actor_UsesInMemory_WhenBuildingEnabled` would fail at `engine.Building.IsEnabled`. **Covered.**
- **Build/this.cs**: No test directly instantiates or exercises `_engine` field. But `IsEnabled` is exercised through Actor tests. Dead `_engine` reference is an architectural placeholder.

### Findings (all minor)

1. **InMemory_CrudOperations is a monolith** — 5 ops in one test. Not a false green, but reduces debugging granularity.
2. **No Engine→Actor→DataSource dispose chain test for in-memory** — standalone InMemory dispose is tested but not through Engine teardown.
3. **No test for both flags true simultaneously** — trivially correct via `||`, not worth testing.
4. **Build.@this._engine is dead code** — correct architectural pattern, no issue.

## Code example

The strongest test pattern — checking side effects, not just return values:

```csharp
[Test]
public async Task Actor_UsesInMemory_WhenTestingEnabled()
{
    await using var engine = new PLangEngine(_tempDir);
    engine.Testing.IsEnabled = true;

    var ds = engine.User.DataSource;
    var setResult = await ds.Set("test_table", "k", "v");
    await Assert.That(setResult.Success).IsTrue();

    // Verify no .db directory was created on disk
    var dbDir = System.IO.Path.Combine(_tempDir, ".db");
    await Assert.That(System.IO.Directory.Exists(dbDir)).IsFalse();  // <-- intent verification
}
```

This checks what *didn't happen* (no disk write) — far stronger than checking what the code returns.
