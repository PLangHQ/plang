# Security Analysis v1 — Builder Module

## What this is

Security audit of the builder module (`App.modules.builder`) — the new PLang module that replaces `[plang]` built-in calls with standard action-based builder operations. Covers Goal.Parse(), 8 builder actions, EngineModules registry, provider registry, and assembly loading.

## What was done

### Phase 1: Blue Team
Mapped 7 attack surface areas: goal-parser, pr-deserialization, action-registry, provider-registry, assembly-loading, builder-authorization, merge-pipeline.

**Strong points:**
- BuildingGuard is consistent across all 8 actions — correct authorization gate
- File I/O through engine.RunAction (no direct System.IO) — correct abstraction
- JsonException caught at both deserialization sites (MergePrData and App)
- System.Text.Json — no polymorphic deserialization risk
- PrPath derived from convention (Goal.Path → `.build/name.pr`), not user-supplied

### Phase 2: Red Team
5 findings total:

| # | Severity | Area | Summary |
|---|----------|------|---------|
| 1 | Medium | Goal.Parse() | No input size limit — OOM on pathologically large .goal files |
| 2 | Medium | Providers.ResolveType | Null/empty type silently defaults to ISigningProvider |
| 3 | Low | Error messages | File paths and exception details in build output |
| 4 | Low | Step.Merge() | Actions copied by reference, not deep clone |
| 5 | Low | GetDefaults | Bare catch swallows Activator.CreateInstance exceptions |

### Verdict: PASS

No critical or high findings. The builder module follows the PLang threat model correctly — .goal files and .pr files are user-authored trusted content, and the builder runs only during the build phase on the developer's machine.

## Key files reviewed

- `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs` — all 8 provider methods
- `PLang/App/Goals/Goal/this.cs` — Goal.Parse(), MergeFrom()
- `PLang/App/Goals/Goal/Steps/Step/this.cs` — Step.Merge()
- `PLang/App/Modules/this.cs` — Discover, Describe, GetDefaults
- `PLang/App/Providers/this.cs` — provider registry, ResolveType
- `PLang/App/modules/module/add.cs` — assembly loading
- `PLang/App/modules/provider/load.cs` — provider DLL loading
- All builder action records (actions.cs, goals.cs, validate.cs, merge.cs, app.cs, appSave.cs, goalsSave.cs, types.cs)

## Recommendation

Pass to the **auditor** for final review.
