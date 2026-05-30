# coder v3 — Stage 3: kind canonicalisation + Format.KindOf→FamilyOf

## Scope

1. Rename `App.Format.KindOf` → `FamilyOf` across PLang + tests. The formats
   registry called the family the "kind" — under the new vocabulary the
   family is the type's *name*, and the kind is the subtype, so the method
   name now matches what it returns.
2. Add `App.Format.CanonicaliseKind(string)` — derived from the
   extension→MIME registry. Accepts long/short forms (`markdown` → `md`,
   `jpeg` → `jpg`). Shared MIME-subtype wins on shortest extension
   (`.jpg`/`.jpeg` → `image/jpeg` → primary `jpg`). Unknown free strings
   pass through.
3. Wire the canonicaliser through `type.@this.Create` — when a context is
   provided, the factory canonicalises the kind via `App.Format.CanonicaliseKind`.

## Done

- `KindOf` → `FamilyOf` in `PLang/app/format/list/this.cs` (the
  implementation) and in 2 internal callers (`type.@this.Compressible`,
  `Data.Wrap`), plus ~36 test sites updated via sed.
- New `CanonicaliseKind` method on `app.format.list.@this` — walks
  `_extensionToMime`, finds entries whose MIME subtype matches the input,
  picks the shortest extension as primary, strips the `.` prefix.
- `type.@this.Create(name, kind, strict, context?)` now takes an optional
  context; when present, kind is canonicalised via
  `context.App.Format.CanonicaliseKind` before being stored.
- Test bodies written: `FamilyOfRenameTests` (4) + `KindCanonicalisationTests` (6).

## Results

- C# total: 3803 (unchanged).
- C# passing: **3731** (vs 3721 after Stage 2, vs 3732 after Stage 1, vs
  3696 baseline). +10 vs Stage 2.
- C# failing: 72 — all Stage 4–5 stubs plus the pre-existing
  `EngineTypesTests`/`TypeMappingTests` that still assert old `int`/`string`
  names (Stage 2 churn that wasn't followed through).
- PLang: 253/253 + 10 stale (unchanged).

## What's next

- **Stage 4** — `variable.set.Type` becomes a `type` (not `Data<string>?`);
  strict `ValidateBuild` calls `IKindValidatable`; `image.ValidateKind` body
  via ImageSharp `Identify`; PLang `.test.goal` bodies + GIF/PNG fixtures.
  Largest remaining piece — structural change to the variable.set handler.
- **Stage 5** — LLM prompt restructure (Compile.llm vocabulary block,
  TypeSchemas dual-mode renderer, drop the `Primitive types:` line, teach
  `type(name, kind?, strict?)`).
