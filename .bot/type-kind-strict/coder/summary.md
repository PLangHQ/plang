# coder — type-kind-strict

## Version
v1

## What this is

Architect carved a 5-stage plan to reshape the PLang type value into a
structured `{Name, Kind, Strict}` entity. Test-designer wrote 107 C# + 10
PLang failing-test stubs across all 5 stages. **v1 implements Stage 1 only**
— per the test-designer and architect's strict-order rule: each stage must
leave both suites green before the next begins.

Stage 1 = the data-model spine. Everything else hangs off it.

## What was done

1. **`app.type.@this` reshape** (`PLang/app/type/this.cs`)
   - Renamed `Value` → `Name`.
   - Added `Kind` (`string?`, mutable, `[JsonIgnore]`) — the build-time
     subtype refinement, single owner.
   - Added `Strict` (`bool`, init-only, `[JsonIgnore]`) — gates strict
     kind-enforcement; not on the wire.
   - Dropped the family-`Kind` accessor (the one that read
     `App.Format.KindOf(Value)`). The family is now the `Name`; callers that
     wanted family-derivation switch to `App.Format.KindOf(Name)` directly.
   - `Compressible` re-derives via `Format.KindOf(Name)` → `Format.Compressible(...)`.
   - `ClrType` moved from public to `internal` — same-assembly callers still
     read it directly; the public PLang surface is name-keyed.
   - Added `static Create(name, kind?, strict?)` normalising factory:
     lowercases the name, splits a single-string slash form on the first `/`,
     rejects empty/whitespace names.

2. **`Data.Kind` fold** (`PLang/app/data/this.cs`)
   - Deleted the stored field. Getter reads `Type.Kind`; setter writes
     through (no-op on the `Null` sentinel since the sentinel is a shared
     singleton).

3. **Wire** (`PLang/app/data/Wire.cs`)
   - Writes `kind` from `data.Type.Kind`.
   - Reads `kind` and hydrates it onto `Type.Kind`. Legacy `.pr.json` files
     with separate `type`/`kind` top-level keys parse identically — the fold
     is internal; the wire shape is unchanged.

4. **`IKindValidatable` marker** (new file `PLang/app/data/IKindValidatable.cs`)
   - Sibling to `IBooleanResolvable`. Signature
     `(bool ok, string? actualKind) ValidateKind(object value, string requiredKind)`.
   - `image` implements (placeholder body returns `(true, null)`; Stage 4
     fills in the magic-byte sniff).
   - `text` does not implement (no type exists yet — Stage 2 lands it; the
     reflection probe confirms the negative). `number` does not implement.

5. **Dispatcher rename** (`PLang/app/type/list/this.cs`)
   - `App.Type.Kinds` → `App.Type.KindHooks`. Property rename only — the
     `Of(clrType, value)` shape is unchanged. The word "kind" had been
     colliding three ways (per-value subtype, advertised vocabulary,
     build-hook dispatcher); the rename leaves only the first two on the
     entity, and the dispatcher gets its own name.

6. **Fan-out** — `Value`→`Name` callers (~25 source files + tests). `Type.Kind`-
   as-family callers updated to call `App.Format.KindOf(Name)` directly.

## Tests

| Suite | Before | After | Delta |
|-------|-------:|------:|------:|
| C# total | 3803 | 3803 | 0 |
| C# passed | 3696 | 3732 | **+36** |
| C# failed | 107 | 71 | -36 |
| PLang passed | 253 | 253 | 0 |
| PLang stale | 10 | 10 | 0 (Stage 2/4 stubs) |

All 28 Stage 1 stubs now pass:
- `TypeValueModelTests/TypeFactoryTests` (10) ✓
- `TypeValueModelTests/TypeEntityShapeTests` (7) ✓
- `TypeValueModelTests/DataKindFoldTests` (3) ✓
- `TypeValueModelTests/WireKindShapeTests` (6) ✓
- `TypeValueModelTests/IKindValidatableMarkerTests` (5) ✓
- `TypeValueModelTests/ClrTypeRerouteTests` (3) ✓
- `TypeValueModelTests/DispatcherRenameTests` (2) ✓

8 additional Stage 2–5 stubs flip green incidentally (test-designer's
contract pins were already true under Stage 1 alone — e.g. `Aliases_TextStillResolves`,
`Canonical_FloatMapsToNumber`).

The remaining 71 failing tests are Stage 2–5 stubs (`Assert.Fail("Not implemented")`)
plus a handful of Stage 2–dependent tests (`Run_SetAsTextWithReadmeMd_MintTypeIsTextMd`,
`BuildStamp_AgreesWithRuntimeMint`, `Run_SetAsImageGifWithGifBytes_MintTypeIsImageGif`)
that depend on `variable.set.Type` becoming a `type` entity (Stage 4) and
`primitive.Canonical[string]` flipping to `"text"` (Stage 2).

## Pinned contracts (test-designer's open items)

1. **`Factory_EmptyName_Rejected`** — pinned to "throws at factory"
   (`System.ArgumentException`).
2. **`Data_KindSetter_WritesThroughToTypeKind`** — pinned to write-through;
   no-op on the `Null` sentinel.
3. **`Factory_String_CanonicalisesNameToText`** — pinned to return `"string"`
   until Stage 2 flips `primitive.Canonical[typeof(string)]` from `"string"`
   to `"text"`. Test body was written to assert the Stage-1 truth and
   commented the Stage-2 flip. (The test description still says "Canonicalises
   NameToText" because that was the file's eventual contract; coder will
   re-pin to "text" in Stage 2.)

## Code example

```csharp
// Before
var t = new app.type.@this("image/jpeg");
var family = t.Kind;            // "image" (via App.Format.KindOf)
data.Kind = "jpg";               // separate stored field

// After
var t = app.type.@this.Create("image/jpeg");   // splits on slash
// t.Name == "image", t.Kind == "jpeg", t.Strict == false

var family = app.Format.KindOf(t.Name);        // explicit registry call

data.Kind = "jpg";                              // writes through to t.Kind
// data.Type.Kind == "jpg"
```

## What's next

- **Stage 2** (next coder version): create `app/type/text/`,
  flip `primitive.Canonical[typeof(string)] = "text"`, move numeric
  primitives under `number`. Updates `BuilderNames`.
- Then Stage 3 (kind canonicalisation + `Format.KindOf` → `FamilyOf` rename)
- Stage 4 (`variable.set.Type` becomes `type`, strict ValidateBuild + Run,
  `image.ValidateKind` body, PLang `.test.goal` bodies)
- Stage 5 (LLM prompt restructure)

## Hand-off

```
Next: run.ps1 codeanalyzer type-kind-strict "Review coder v1 Stage 1 on branch type-kind-strict" -b type-kind-strict
```

(or: continue to Stage 2 via another coder run.)
