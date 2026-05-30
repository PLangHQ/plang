# test-designer — singular-namespaces

## Version
v1

## What this is

The architect carved a 4-stage plan for the branch: **rename** `PLang/app/**` to singular+lowercase (Stage 1), **non-null `app`/`context`** invariants (Stage 2), **accessor reshape** turning `app.X` into a collection node (Stage 3), and **type entity move + Entry fold** (Stage 4). My job is to translate the architect's coverage matrix + failure matrix + 4 integration cuts into concrete test signatures that pin the contract for the coder.

The branch is dominated by a refactor, so the test strategy is **regression floor + ceiling** (per `architect/plan/test-strategy.md`):

- Floor — both existing C# and PLang suites stay green stage-by-stage. Catches the ~286 call-site migrations and `ctx`→`context` rename without new tests.
- Ceiling — the new C# surface (accessor selectors, non-null invariant, index-miss errors, channel I/O on the element, type entity at `type.@this`, folded Entry fields, builder schema golden) + 5 PLang `.test.goal` files for what's PLang-author-observable.

## What was done

User authorized end-to-end execution. I wrote `v1/plan.md` with the batch breakdown, presented it once for visibility, then proceeded with all 8 batches without per-batch approval.

**Test files written** (all bodies are `Assert.Fail("Not implemented")` / `- throw "not implemented"` — names + comments are the spec):

- **C# (10 files / 52 tests)** under `PLang.Tests/App/SingularNamespaces/`:
  - `AccessorTests/` — Goal (8), Channel (8), Type (9), Module (5), Other accessors + `App*` aliases gone (10).
  - `NullabilityTests/NonNullInvariantTests.cs` (7) — Stage 2 invariant, un-stamped-`data.Type` throws, no static fallback, `app.Parent` nullable, structural back-refs.
  - `TypeEntityTests/TypeEntityHomeTests.cs` (7) — entity at `type.@this`, both doors return same entity, folded Entry fields, `Entry`/`Field`/`EntryKind` dissolved, `data/Converter.cs` deleted.
  - `BuilderSchemaTests/BuilderSchemaGoldenTests.cs` (2) — integration cut 3 (Stage 4 byte-identical schema).
  - `RenameIntegrationTests/BuildAndRunGoalTests.cs` (2) — integration cut 1 (rename proof).
- **PLang (5 `.test.goal` files)** under `Tests/SingularNamespaces/`:
  - `GoalBuildAndRun`, `SubGoalCallResolves`, `ChannelWriteThroughAccessor`, `ChannelIndexMissThrows`, `DataTypeReadsEntity`.

**Open items resolved with test-designer judgment** (per "use your judgment"):
1. Index-miss exception type — left to coder; tests describe shape ("throws a typed error"), not concrete exception.
2. `app.module.current` guard — reflection probe (test method `AppModule_HasNoCurrentMember_ReflectionGuard`).
3. Builder schema golden — coder embeds baseline inline; if `Tests/Types/` already has one, extend it (noted in test comments).
4. Goal-registry-no-I/O — included (symmetric to channel; same registry/element rule).

**Coverage**: every row of the architect's coverage matrix maps to ≥ 1 test. The 4 integration cuts each have a dedicated test. The failure matrix's 6 rows distribute across the accessor batches + nullability + PLang.

**Not done** (deliberately — per architect): no tests for the call-site migrations, `ctx`→`context` rename, doc updates, or the 2 init-only back-refs held nullable. Regression floor covers them.

## Code example

C# test signature (the contract for the coder):

```csharp
// AccessorTests/ChannelAccessorTests.cs
[Test] public async Task ActorChannel_IndexOfUnknownName_ThrowsTypedError()
    => Assert.Fail("Not implemented");

// Polymorphism replaces the registry's `is channel.stream.@this` type-switch.
[Test] public async Task StreamChannel_Write_UsesTheStreamOptimizedOverride()
    => Assert.Fail("Not implemented");
```

PLang test goal:

```
TestUnknownChannelThrowsTypedError
/ Failure matrix: actor.channel["nope"] selecting an absent channel from PLang code raises
/ a typed error — uniform with goal/type index-miss, no silent noop, no implicit create.
- throw "not implemented"
```

## Next

Run **coder** next — implement and make the tests pass. Stage order is strict per `architect/plan.md`: 1 (rename) → 2 (non-null) → 3 (accessor) → 4 (type entity). Each stage must leave both suites green before the next.
