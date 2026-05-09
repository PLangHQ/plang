# coder — runtime2-cleanup

## Version
v25 — Stage 25: `Default.cs` static eviction. (Stage 24 also landed this session — see git log.)

## What this is
Two Tier 5 hygiene stages in one session. Both close Rule C (static fields are a missing `@this`).

- **Stage 24** evicts the byte-identical `_options` static `JsonSerializerOptions` from both `AskCallback.cs` and `ErrorCallback.cs` into a single instance-owned slot on a new `Callback.Wire.@this` subfolder. Closes Rule C + smell #3 (same logical thing duplicated across types). Navigation: `app.Callback.Wire.Options`.
- **Stage 25** evicts the two static `JsonSerializerOptions` fields from `Default.cs` (HTTP code provider). `_jsonOptions` was a degenerate alias for `App.Utils.Json.CaseInsensitiveRead` — deleted, callers expanded to long form (matching the third pre-existing site). `_transportInOptions` was a real local options block — converted to instance field.

## What was done
### Stage 24
- New `PLang/App/Callback/Wire/this.cs` — internal `Options` property holding the shared `JsonSerializerOptions`.
- `PLang/App/Callback/this.cs` — added `Wire` property alongside existing `Signature` (mirrors the OBP shape exactly).
- `AskCallback.cs` — static deleted; both reads → `ctx.App.Callback.Wire.Options`. Two unused usings dropped.
- `ErrorCallback.cs` — static deleted; private static helpers `SerializeSnapshot` / `DeserializeSnapshot` thread `JsonSerializerOptions` through as a parameter. Two unused usings dropped.

### Stage 25
- `_jsonOptions` alias deleted; 2 reads switched to `App.Utils.Json.CaseInsensitiveRead` long-form.
- `_transportInOptions` static → instance.
- **Brief deviation:** the 3 read sites were in `private static async` helpers (not instance methods as the brief assumed). Cleanest fix: convert 5 helpers (`ParseResponseAsync`, `ParsePlangResponseAsync`, `TryExtractSignedErrorIdentity`, `HandleStreamingAsync`, `StreamPlangAsync`) from `static` to instance. Callers are inside lambdas in instance methods, so call sites need no change. The alternative — threading the options through 5 signatures + 5 call sites — was heavier for no semantic benefit.

## Code example
Stage 24 Wire @this (mirrors Signature shape):
```csharp
namespace App.Callback.Wire;

public sealed class @this
{
    internal JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ...
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { App.Channels.Serializers.Filters.Sensitive.Strip }
        }
    };
}
```

Stage 25 callers don't change at all — only `static` is dropped from method signatures:
```csharp
// Before
private static async Task<Data.@this> ParsePlangResponseAsync(...)
// After
private async Task<Data.@this> ParsePlangResponseAsync(...)
```

## Stage closure
- C# tests green: 2752/2752 (both stages)
- PLang tests green: 199/199 (both stages)
- Stage 24: zero `private static readonly JsonSerializerOptions` in Callback files; new file present; Wire mounted; 4 navigation reads in callers
- Stage 25: zero `private static readonly` in `Default.cs`; zero `_jsonOptions`; 4 hits of `_transportInOptions` (1 decl + 3 reads); 3 hits of `CaseInsensitiveRead`
- Behaviour change: none on either stage.

Stage 26 (`types-keystone`) deferred to next session per architect's note ("probably needs its own session").
