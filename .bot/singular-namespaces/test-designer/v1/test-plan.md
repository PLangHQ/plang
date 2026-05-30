# Test Plan — singular-namespaces v1 (approved)

User authorized end-to-end execution ("use your judgment, do all batches"). All 8 batches written without per-batch approval gates. Open items resolved with test-designer judgment:

1. **Index-miss exception type** — left to coder to pin; tests describe shape ("throws a typed error"), not the concrete exception.
2. **`app.module.current` guard** — implemented as a reflection probe (`HasNoCurrentMember_ReflectionGuard`).
3. **Builder schema golden** — coder embeds the baseline inline in the test file; if a `Tests/Types/` golden already exists, extend it (noted in test comment).
4. **Goal-registry-no-I/O test** — included (symmetric to channel; same registry/element rule).

## Files written

### C# (10 files, 52 tests) — `PLang.Tests/App/SingularNamespaces/`

| File | Tests | Batch | Stage |
|---|---:|---|---|
| `AccessorTests/GoalAccessorTests.cs` | 8 | A | 3 |
| `AccessorTests/ChannelAccessorTests.cs` | 8 | B | 3 |
| `AccessorTests/TypeAccessorTests.cs` | 9 | C | 3+4 |
| `AccessorTests/ModuleAccessorTests.cs` | 5 | D | 3 |
| `AccessorTests/OtherAccessorsTests.cs` | 10 | D | 3 |
| `NullabilityTests/NonNullInvariantTests.cs` | 7 | E | 2 |
| `TypeEntityTests/TypeEntityHomeTests.cs` | 7 | F | 4 |
| `BuilderSchemaTests/BuilderSchemaGoldenTests.cs` | 2 | G | 4 |
| `RenameIntegrationTests/BuildAndRunGoalTests.cs` | 2 | H | 1+3 |

### PLang (5 `.test.goal` files) — `Tests/SingularNamespaces/`

| File | Surface | Stage |
|---|---|---|
| `GoalBuildAndRun.test.goal` | Cut 1 — rename proof in PLang | 1 |
| `SubGoalCallResolves.test.goal` | Cut 1 sibling — goal.call after accessor reshape | 1+3 |
| `ChannelWriteThroughAccessor.test.goal` | Cut 2 — `write to %channel%` lands on `channel.@this.Write` | 3 |
| `ChannelIndexMissThrows.test.goal` | Failure matrix — unknown channel surfaces typed error to PLang | 3 |
| `DataTypeReadsEntity.test.goal` | Stage 4 — `%x.Type.Name`/`ClrType` after entity move | 2+4 |

## Coverage mapping (architect matrix → test signatures)

Every row of `architect/plan/test-coverage.md` maps to at least one test. Cross-cutting failure-matrix rows distributed across A/B/C/E/H. The 4 integration cuts:

| Cut | Location |
|---|---|
| 1 (build+run goal) | `BuildAndRunGoalTests.cs` + `GoalBuildAndRun.test.goal` |
| 2 (channel I/O through accessor) | `ChannelAccessorTests.cs` (round-trip + stream override) + `ChannelWriteThroughAccessor.test.goal` |
| 3 (builder schema golden) | `BuilderSchemaGoldenTests.cs` |
| 4 (un-stamped `data` throws) | `NonNullInvariantTests.cs` `DataType_OnUnstampedData_ThrowsHard_...` |

## What's NOT in this contract (regression floor)

- ~286 individual call-site migrations.
- `ctx`→`context` rename (214 identifiers, 36 files).
- The 2 init-only back-refs held nullable (`GoalCall.Action`, `IEvent.Step`).
- Folder structure and namespace decl updates (proven by the build going green + cut 1).
- Doc reference updates.

## Notes for the coder

- Test bodies are `Assert.Fail("Not implemented")` (C#) / `- throw "not implemented"` (PLang) per the test-designer contract. Replace with real assertions as each stage lands.
- `BuilderSchemaGoldenTests` — check for an existing golden under `Tests/Types/` before writing fresh.
- `AppStarAliases_..._NoLongerExist` and `BuilderTypesEntry_..._DoNotExist_AfterFold` are reflection-probe negatives — assert `Type.GetType("...") == null` against the relevant assemblies, OR scan source. Pick whichever is cheaper.
- Index-miss tests use `Assert.Throws<Exception>` for now — pin the concrete type once decided (see open item 1 above).
