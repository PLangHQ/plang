# Auditor v1 Summary — In-Memory SQLite DataSource

## What this is

Code integrity review of the in-memory SQLite datasource feature. The coder added `SqliteDataSource.InMemory(name)` with a sentinel connection pattern, a `Building` object on Engine (following the Test/Debug pattern), and automatic in-memory selection in `Actor.CreateDataSource()` when Testing or Building is enabled.

## What was done

### Review approach

Isolated the coder's actual commit (`62e50ae8`) from the large diff against `runtime2` — the branch diverged before several runtime2 merges (setup-goal system, terminology rename, SettingsData sharing fix), creating merge gap noise in the full diff. The coder's commit is clean: 6 files, ~50 lines of production code, 7 tests.

### Findings

**Finding 1 (major, merge-gap): SettingsData sharing** — `PLang/App/Context/Actor.cs:64`

The branch diverged from runtime2 before commit `af3b34a9` ("Share SettingsData across all actors — fix %Settings.X% unreachable from PLang code"). On this branch, SettingsData is only on the System actor's Variables. On runtime2, it's shared across all actors. When merging, the coder must preserve runtime2's shared pattern while adding the in-memory check. This is NOT a coder regression — it's a merge gap.

**Finding 2 (minor, carry-forward): DeserializeValue exception gap** — `SqliteDataSource.cs:282`

Already flagged by the security report. `DeserializeValue` catches `JsonException` but not `InvalidOperationException` from `Data.UnwrapJsonElement` depth guard. Currently unreachable but violates "behavior methods never throw" contract.

**Finding 3 (nit): Unused constructor parameter** — `SqliteDataSource.cs:46`

`bool inMemory` parameter exists only for constructor disambiguation. Works but could be clearer.

### What passed review

- **Sentinel pattern**: Correct. Shared-cache in-memory DB needs one connection open to survive. Sentinel opens at construction, closes at Dispose. Connection-per-operation pattern works because all connections share the same in-memory DB via shared cache.
- **Dispose lifecycle**: Correct order — sentinel closes before pool clear. Idempotent via `_disposed` flag.
- **OBP compliance**: Actor navigates to `Engine.Testing.IsEnabled` and `Engine.Building.IsEnabled` (rule 2). SqliteDataSource owns its sentinel lifecycle (rule 1). Building follows Test/Debug pattern exactly.
- **Test quality**: Actor integration tests verify disk side-effects (`System.IO.Directory.Exists(dbDir)`) not just return values — gold standard for verifying in-memory mode. Dispose test verifies sentinel lifecycle end-to-end.

### Verdict: PASS

The coder's code is ready to merge, but the merge itself requires careful conflict resolution in `Actor.cs` and `Engine/this.cs` to preserve the SettingsData sharing fix from runtime2.

## Code example

The merge conflict in Actor.cs needs to combine both changes:

```csharp
// runtime2 has: shared SettingsData for all actors
Context.Variables.Put(engine.SettingsVariable);

// This branch has: in-memory check in CreateDataSource
private IDataSource CreateDataSource()
{
    if (Engine.Testing.IsEnabled || Engine.Building.IsEnabled)
        return SqliteDataSource.InMemory(Name.ToLowerInvariant());
    // ...file-backed path...
}
```

Both must be preserved in the merge.

## Files reviewed

| File | Assessment |
|---|---|
| `PLang/App/DataSource/SqliteDataSource.cs` | Clean. Sentinel pattern correct. |
| `PLang/App/Build/this.cs` | Clean. Follows Test/Debug pattern. |
| `PLang/App/this.cs` | Clean. Building wired correctly. |
| `PLang/App/Context/Actor.cs` | Clean code, merge gap with runtime2. |
| `PLang/App/GlobalUsings.cs` | Clean. Alias conflict documented. |
| `PLang.Tests/App/Modules/datasource/DataSourceTests.cs` | 7 new tests, good intent verification. |
