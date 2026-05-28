# coder v1 — data-normalize Stage 1

**Status:** Stage 1 landed. C# 3274/3274 real tests pass (105 known-failing Stage 2/3 stubs from test-designer v1, unchanged in count). PLang 228/228 pass. Stages 2 and 3 pending.

## What landed

### `[Masked]` attribute (new)
`PLang/app/View.cs:50-60` — sealed property-target marker that joins `[Out]`, `[Sensitive]`, `[In]`, `[Store]`, `[LlmBuilder]`, `[Debug]`, `[Default]`. Stage 1 only tags `setting.value`; the actual `"****"` masking behavior lands in Stage 2's Normalize walker.

### `Data.RawSignature` deletion (+ 7 caller migrations)
Property at `PLang/app/data/this.Transport.cs:46` deleted. Since stage 2a.7 removed the lazy-populate side-effect on `Signature.get`, `RawSignature` was a pure duplicate accessor. Seven sites migrated to `Signature` directly:

- `PLang/app/data/WireJsonConverter.cs:272, 309, 312`
- `PLang/app/actor/permission/this.cs:94, 139`
- `PLang/app/modules/signing/code/Ed25519.cs:65, 68`

Also updated nine bystander C# test files that read `data.RawSignature` for the same "peek without lazy populate" intent — semantics are identical post-stage-2a.7, so `Signature` is the single accessor.

### `[Out]` discipline (13 domain types)

Per `plan/wire-out-attributes.md`:

| Type | File | Properties marked `[Out]` |
|------|------|---------------------------|
| Identity | `PLang/app/modules/identity/types.cs` | Name, PublicKey |
| path (base) | `PLang/app/types/path/this.cs` | Scheme, Relative |
| StatInfo | `PLang/app/types/path/this.Operations.cs` | Exists, IsFile, Length, Modified |
| list | `PLang/app/modules/list/types.cs` | count, value |
| Variable | `PLang/app/variables/Variable.cs` | Name |
| Data | `PLang/app/data/this.cs` + `this.Result.cs` | Value, Type, Properties, Success, Error (Signature already had `[Out]`) |
| GoalCall | `PLang/app/goals/goal/GoalCall.cs` | Name, Parallel, Parameters, PrPath |
| permission | `PLang/app/types/path/permission/this.cs` | Actor, Path, Verb, Match |
| setting | `PLang/app/modules/settings/types.cs` | key, value (+ `[Masked]` on value) |
| http.Response | `PLang/app/http/Response/this.cs` | Status, Headers, Body |
| Ask | `PLang/app/modules/output/ask.cs` | Answer |
| Mock | `PLang/app/mock/Mock/this.cs` | none — test-time-only type |
| condition.Operator | `PLang/app/modules/condition/Operator.cs` | Value |

## Required side-effect: `Transport.ForOutbound` converter preservation

Adding `[Out]` to `Data.Type` exposed a bug in `app.channels.serializers.filters.Transport.ForOutbound`. The filter removes `[JsonIgnore]`'d properties and recreates a `JsonPropertyInfo` for any property tagged `[Out]`. The recreation passed only `prop.PropertyType` to `CreateJsonPropertyInfo`, dropping the property's `[JsonConverter(typeof(data.Json))]` attribute. That converter renders `data.type` as a plain JSON string; without it, STJ tried the default object path on `data.type` and hit `ClrType` (a `System.Type`), throwing `NotSupportedException`.

This was latent — Signature was the only `[Out]` property and didn't carry a custom converter. Stage 1's widened `[Out]` set tripped it for three pre-existing tests (`TransportPropertyFilterTests.{Roundtrip_SignaturePreserved_ThroughSerializeDeserialize, ForOutbound_SerializesSignature_DespiteJsonIgnore, ForOutbound_NullSignature_OmittedFromJson}`).

Fix at `PLang/app/channels/serializers/filters/Transport.cs:62-68`: copy any property-level `[JsonConverter]` onto the recreated `JsonPropertyInfo.CustomConverter`. Pure preservation of existing per-property serialization behavior; no contract change.

The architect's Stage 1 plan said "No behavior change yet. The existing JSON serializer still works because `[Out]` is already what the JSON path consults." The qualifier needs a footnote: it works *given the converter-preservation fix*. Noting for the architect's next reconciliation pass.

## Tests added

Three contract test files turned from `Assert.Fail` stubs into real assertions:

- `PLang.Tests/App/Serialization/OutAttributeInventoryTests.cs` — 31 reflection assertions, one per (type, property) decision in the inventory. Includes the `Mock_NoOutProperties_TestOnlyType` sweep that asserts Mock stays empty.
- `PLang.Tests/App/Serialization/RawSignatureDeletionTests.cs` — 6 tests: reflection on `typeof(Data).GetProperty("RawSignature")` returning null, plus string-scans of the three migrated source files for any `RawSignature` residue. Mirrors the precedent set by `data-serialize-cleanup`.
- `PLang.Tests/App/Serialization/MaskedAttributeTests.cs` — 5 tests: attribute exists, sealed, property-target only, coexists with `[Out]` on `setting.value`, `setting.key` has `[Out]` but not `[Masked]`.

One pre-existing test inverted to match the new contract: `PLang.Tests/App/DataTests/DataTests.cs:Properties_HasNoOutAttribute` → `Properties_HasOutAttribute` (the wire-out-attributes inventory promotes Properties from `[JsonIgnore]`-only to `[Out]` per architect note: "the property already ships via WireJsonConverter's custom Write — the tag just makes the new filter see it correctly").

## Test results

**C# (`dotnet run --project PLang.Tests --no-build`)**
- Total 3379, passed 3274, failed 105 (all `AssertionException: Not implemented` — Stage 2/3 test-designer stubs).
- Stage 1 contract tests (42 of 42) pass: 31 OutAttributeInventory + 6 RawSignatureDeletion + 5 MaskedAttribute.
- Baseline (architect-time) was 3229/3229. Net +45 added real tests (Stage 1 contracts + Properties_HasOutAttribute inversion), +105 Stage 2/3 stubs.

**PLang (`Tests/`, via `../PlangConsole/bin/Debug/net10.0/plang --test`)**
- 233 total, 228 pass, 0 fail, 5 stale. Same as data-serialize-cleanup baseline. No regressions.

## What's next

Stage 2 (`stage-2-normalize-jsonwriter.md`): `Data.Normalize()` walker, `IWriter` protocol, `JsonWriter` first impl, wire-view filter that enforces `[Out]` as a whitelist, `[Masked]` honored as `"****"`, debug-mode bypass.

The `[Out]` discipline + `[Masked]` attribute is now the stable surface Stage 2 builds the filter on top of. The Normalize-related test stubs already exist in `PLang.Tests/App/DataTests/Normalize*` and `PLang.Tests/App/Serialization/IWriterContractTests.cs` etc. — Stage 2 coder turns those `Assert.Fail` bodies into real assertions as the implementation lands.
