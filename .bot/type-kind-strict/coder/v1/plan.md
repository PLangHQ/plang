# coder v1 — type-kind-strict — plan

## Scope

**Stage 1 only.** Per architect + test-designer: strict order, each stage must leave both suites green before the next. Stages 2–5 are future versions.

Stage 1 deliverables (from `architect/stage-1-type-value-model.md`):

1. `app.type.@this`: rename `Value`→`Name`, add `Kind` (`string?`), add `Strict` (bool), drop family-`Kind` accessor, internalise `ClrType`. Add normalising factory `Create(name, kind?, strict?)` with single-string slash split.
2. `Data.Kind`: stored field deleted; reads/writes route through `Type.Kind`.
3. `Wire.Write`: serialise `kind` key from `Type.Kind` (not from a stored field). `Wire.Read`: still accepts legacy `{type, kind}`.
4. New marker `IKindValidatable` in `app/data/`. `image` implements (placeholder — stage 4 wires the byte sniff).
5. Rename dispatcher `App.Type.Kinds` (`app.type.kind.@this`) → `App.Type.KindHooks`. (Property rename; method shape unchanged.)
6. `ClrType` consumers outside `app.type.*`/`app.data.*`: reroute via `App.Type.Clr(name)` / `Get(name)`.

## Targets pinned (test-designer's open items)

- `Factory_EmptyName_Rejected` — **throw** at factory (matches the pinned contract).
- `Data_KindSetter_Contract` — write-through to `Type.Kind` (least surprising for current callers).
- Strict on `text` (unverifiable) — accepted at construction; validator skips byte-check; pinned in stages 3/4 not here.

## Test-set this version must turn green

All 28 tests in `PLang.Tests/App/TypeKindStrict/TypeValueModelTests/`:

- `TypeFactoryTests` (10)
- `TypeEntityShapeTests` (7)
- `DataKindFoldTests` (3)
- `WireKindShapeTests` (6)
- `IKindValidatableMarkerTests` (5)
- `ClrTypeRerouteTests` (3)
- `DispatcherRenameTests` (2)

Plus: no regression in the existing 3696 passing tests.

PLang test stubs in `Tests/TypeKindStrict/` are stage 2+'s deliverable — they stay stale in v1.

## Approach

Two passes:

**Pass A — entity shape**
- Rewrite `PLang/app/type/this.cs`:
  - `Name` (was `Value`), `Kind`, `Strict` as the identity fields.
  - Keep `[JsonPropertyName("name")]` on `Name` (wire-stable).
  - Drop public `ClrType`; expose `internal` for the entity's own internal use (`Convert`); registry-side lookups continue via `App.Type.Clr(name)`.
  - Drop the family-`Kind` (the `App.Format.KindOf` accessor) — `Compressible` re-derives from family (use `App.Format.Compressible(name)` directly; family ≡ name now).
  - Static `Create(name, kind?, strict?)` factory: alias-table normalisation (registry on Context if available, else primitive Aliases dictionary), slash split, empty/whitespace rejection.
  - `Null` sentinel uses `Name="null"`.
  - All other catalog props (`Fields`/`Values`/…) untouched.
- Add `IKindValidatable` interface in `PLang/app/data/IKindValidatable.cs`.

**Pass B — fan-out**
- `PLang/app/data/this.cs`: drop stored `Kind` field; expose `Kind` as get/set delegating to `Type.Kind`.
- `PLang/app/data/Wire.cs`: write `kind` key from `data.Type?.Kind`; read still hydrates `Type.Kind`.
- Rename `App.Type.Kinds` → `App.Type.KindHooks` (one property in `app.type.list.@this`).
- Reroute 3 module sites (`module/file/read.cs`, `module/variable/set.cs`, `module/settings/Sqlite.cs`) from `Type.ClrType` to `App.Type.Clr(name)`.
- Update ~16 `.Type.Value` callers in builder / module code to `.Type.Name`.
- Add `IKindValidatable` placeholder on `image` (stage 4 will provide the byte sniff).

## Verification

1. `dotnet build PlangConsole` green.
2. `dotnet build PLang.Tests` green.
3. `dotnet run --project PLang.Tests` — Stage 1's 28 tests pass; pre-existing 3696 still pass; remaining 79 Stage 2–5 stubs continue to `Assert.Fail`. Net pass count: 3724 / 3803 (3696 + 28).
4. `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` — 253/253 still pass, 10 Stage 4 PLang stubs still stale (not Stage 1's deliverable).

## Out of scope (do NOT touch in v1)

- Stage 2: `text` type, `string`→`text` canonical, numerics-under-`number`. Factory canonicalisation hook is added but the alias-table change is stage 2.
- Stage 3: `KindOf`→`FamilyOf` rename, kind canonicaliser, `image.ValidateKind` body.
- Stage 4: `variable.set.Type` becoming `type`, strict ValidateBuild.
- Stage 5: LLM prompt restructure.
- PLang `.test.goal` bodies under `Tests/TypeKindStrict/`.
