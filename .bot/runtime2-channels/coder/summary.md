# coder — runtime2-channels

## Version
v2

## What this is

Standardize how closed-set parameter types (Actor, Operator, future
channel-name slots) declare their LLM vocabulary. Build-time validation
membership-checks against that vocabulary instead of trying to construct
the type from a string — a path that broke for stateful runtime types
like Actor and was the gate keeping all 14 channel `.test.goal`
scenarios from building.

## What was done

One declarative convention: `[Choices] static string[] Choices(Context?)`
on any type the LLM emits a name for.

- `PLang/App/Attributes/ChoicesAttribute.cs` — attribute
- `PLang/App/Choices/this.cs` — assembly-scan registry, built lazy on
  first use, validates method shape at startup
- `PLang/App/Actor/this.cs` — `ValidValues` → `[Choices] Choices(...)`
- `PLang/App/modules/condition/Operator.cs` — same; drops `: IObject`
- `PLang/App/modules/IObject.cs` — **deleted** (no remaining users)
- `PLang/App/Utils/TypeMapping.cs` — routes `GetValidValues` through
  registry; `GetTypeName` no longer scans for ValidValues
- `PLang/App/Utils/TypeConverter.cs` — IObject branch removed
- `PLang/App/modules/builder/validateResponse.cs` — Choices membership
  check short-circuits ahead of TryConvertTo for [Choices] types
- 3 test files updated to call the new convention

Resolution stays type-local: Actor → `App.GetActor(name)`, Operator →
its registry-validating ctor.

## Code example

```csharp
public sealed class @this    // App.Actor
{
    [Choices]
    public static string[] Choices(Context.@this? ctx) => ["user", "system"];
}
```

In `validateResponse`:

```csharp
var choices = App.Choices.@this.Get(targetType);
if (choices != null)
{
    if (p.Value is string sv && choices.Any(c =>
        string.Equals(c, sv, StringComparison.OrdinalIgnoreCase)))
        continue;
    errors.Add($"... is not a valid {p.Type.Value}. Valid values: {string.Join(", ", choices)}.");
    continue;
}
```

## Test status

- **C#**: 2744 pass, 0 fail (was 2745; -1 is the deleted
  `ImplementsIObject` test for the now-deleted interface).
- **PLang**: 191 pass, 10 fail, 5 stale.
  - Baseline: 188 pass, 0 fail, 18 stale.
  - Stale dropped 13 → channel `.test.goal` bodies build now.
  - 1 channel scenario remains stale: `Add/WithConfig` — LLM
    consistently splits the long step into 5 instead of 4. Separate
    step-splitting concern, not validation.
  - 10 channel scenarios now `[Fail]` at runtime — they build and
    run, exposing real issues in the test bodies or in channel
    runtime behavior. Not validator-related; surfaced by unblocking
    the build.
  - 4 pre-existing Callback stales remain (pre-existing per baseline,
    not this branch's scope).

## What's next — scoped as v3

Debug session classified the 10 fails into 3 root-cause groups, all
architectural:

1. `channel.add`/`set` resolve helper goals via `app.Goals.Get(name)`
   — registry-only, no lazy load. Should be `Data<GoalCall>`-typed
   slot like every other goal-calling action; builder stamps PrPath.
2. `Channels.Resolve` throws `ChannelNotFoundException` instead of
   returning `Data`. `Step.RunAsync` then flattens to
   `Key="StepError"` — destroys the typed key the on-error handler
   matched.
3. The `Channel.Role` enum splits channels into "role" and "custom"
   without any behaviour requiring the split. Just convenience
   defaults around the names "output", "error", "input".

(3) is the real refactor and absorbs (1) and (2). v3 does the channel
cleanup: delete `Role` enum, `set`/`add` collapse to `set`, channels
are uniform, `Channels.@this.Defaults` (a small static name list) +
`Channels.@this.Verify()` replace the role-channel boot invariant.
See `v3/plan.md` for the full plan.
