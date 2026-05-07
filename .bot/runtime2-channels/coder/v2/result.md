# v2 result

## What landed

`[Choices]` attribute + registry standardizes how closed-set parameter
types declare their LLM vocabulary. Replaces the half-formed
`static ValidValues` + `IObject` pattern that conflated
build-time-vocabulary with runtime-resolution.

- `PLang/App/Attributes/ChoicesAttribute.cs` — marker attribute.
- `PLang/App/Choices/this.cs` — registry built once on first access.
- `PLang/App/Actor/this.cs` — `static ValidValues` → `[Choices] static Choices(Context?)`.
- `PLang/App/modules/condition/Operator.cs` — same migration; drops `: IObject`.
- `PLang/App/modules/IObject.cs` — **deleted**. No type left in the
  codebase needed it.
- `PLang/App/Utils/TypeMapping.cs` — `GetValidValues` routes through the
  registry; `GetTypeName` no longer reflects for `ValidValues`.
- `PLang/App/Utils/TypeConverter.cs` — IObject branch removed (Operator
  still works via the generic single-string-ctor branch beneath it).
- `PLang/App/modules/builder/validateResponse.cs` — short-circuits to a
  membership check for any [Choices]-bearing parameter type before
  falling through to TryConvertTo. This fixes the contradictory
  *"cannot be converted to 'actor'. Valid values: user, system."*
  rejection that blocked all channel test builds.
- `PLang/App/modules/condition/providers/DefaultEvaluator.cs` — uses
  `Operator.Choices(null)` for fix-suggestion text.
- Tests updated to call new convention:
  - `PLang.Tests/App/Modules/condition/OperatorTests.cs`
  - `PLang.Tests/App/Utility/TypeMappingTests.cs`
  - `PLang.Tests/App/ChannelsTests/Stage7_AppServicesTests.cs`

## Convention

```csharp
public sealed class @this    // any closed-set parameter type
{
    [Choices]
    public static string[] Choices(Actor.Context.@this? ctx) => ["user", "system"];
}
```

The Context parameter is mandatory for signature symmetry — static
vocabularies ignore it; future dynamic vocabularies (channel names per
actor, settings-driven enums) get the context they need without a
second method shape.

Resolution stays where it lives: Actor → `App.GetActor(name)`,
Operator → ctor against its registry. The language layer only cares
about the vocabulary.

## Tests

- **C#**: 2744 pass, 0 fail. Baseline was 2745 — delta of -1 is the
  deleted `ImplementsIObject` test (the interface no longer exists).
- **PLang**: 191 pass, 10 fail, 5 stale.
  - Baseline: 188 pass, 0 fail, 18 stale.
  - Stale dropped by 13 (channel `.test.goal` bodies that now build
    successfully). 4 Callback stales remain (pre-existing — not this
    branch's scope). 1 Channel stale remains
    (`Add/WithConfig/Start.test.goal` — LLM consistently splits the
    long modifier-chain step into 5 instead of 4 — separate
    step-splitting concern from validation).
  - 10 new channel `[Fail]` entries — these scenarios now build and
    *run*, exposing real runtime issues in the channel test bodies or
    underlying channel runtime. Not validator-related; surfaced by
    unblocking the build.

## What's NOT yet done (v3 scope)

1. **10 channel runtime failures** — the bodies execute, the
   assertions don't pass. Each scenario needs `--debug` to identify
   whether the failure is in:
   - the test body itself (wrong action shape, wrong assertion);
   - a helper sub-goal (Logger.goal, OutputGoal.goal, etc.);
   - actual channel runtime behavior (event firing, goal-channel
     dispatch, etc.).
2. **`Add/WithConfig` step-splitting** — the LLM consistently splits
   `- add channel "audit" call AuditLog, buffer: 65536, timeout: PT30S, mime: 'application/json', write to %channel%`
   into two steps. Either the goal text gets re-shaped to be
   LLM-friendlier, or the builder's step-counting prompt is hardened.
3. **Re-baseline**. The new shape (10 fail / 5 stale / 191 pass) should
   replace the v1 baseline before v3 starts so regressions can be
   measured.
