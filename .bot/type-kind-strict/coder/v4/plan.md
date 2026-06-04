# coder v4 — Stage 4: variable.set takes type + image.ValidateKind body + strict

## Scope

1. `variable.set.Type` parameter shape: `data.@this<string>?` →
   `data.@this<app.type.@this>?`. The handler reads the whole type entity
   and stamps it (kind included) onto the minted variable — fixes the
   dropped-kind bug by construction.
2. Strict enforcement: `ValidateBuild` (build-time, literals) and `Run`
   (runtime, after %var% resolution) both call `IKindValidatable.ValidateKind`
   when `Type.Value.Strict && Type.Value.Kind != null` and the resolved CLR
   type implements the marker. Mismatch → typed error.
3. `image.ValidateKind` body: ImageSharp's `DetectFormat` sniffs the bytes;
   shortest extension wins as the actual kind name; case-insensitive
   compare to the required kind.
4. Helper: `TryInstantiateValidator(targetType, rawValue)` — finds a
   constructor whose first parameter accepts the raw value (matches
   image's `byte[]`-first ctor) and instantiates with defaults for the
   rest. Types without a fitting ctor are treated as "no probe available"
   — strict degrades silently.

## Done

- `variable.set.Type` is now `data.@this<global::app.type.@this>?`.
- `Run()` reads `typeEntity = Type.Value`, resolves `typeEntity.Name`,
  optionally runs strict validation against the byte-sniff seam, and
  stamps `typedData.Type = typeEntity` (the whole entity, not just the name).
- `ValidateBuild` runs the same strict check at build for literals;
  `value.HasVariableReference` short-circuits to runtime.
- `image.@this.ValidateKind` does the real ImageSharp probe.
- Tests: `VariableSetTypeParamTests` (2) + `ImageValidateKindTests` (4)
  with minimal 1×1 GIF/PNG byte arrays embedded.

## Results

- C# total: 3803.
- C# passing: **3734** (vs 3731 after Stage 3, +3).
- Stage 4 remaining stubs: `StrictValidateBuild*`, `StrictRun*`,
  `SetMintCarriesKind*` (8 tests) — these depend on running the full
  variable.set Run() in a test harness, which needs more scaffolding than
  the time budget allows. They stay stale.
- The PLang `.test.goal` bodies in `Tests/TypeKindStrict/` (10 stale) also
  remain to be written — they need `.goal` syntax for the new `as <type>
  strict` clause, which is a builder-level surface change.
- PLang: 253/253 + 10 stale (unchanged).

## What's next

- **Stage 5** — LLM prompt restructure (Compile.llm vocabulary block,
  TypeSchemas dual-mode renderer, drop the `Primitive types:` line, teach
  `type(name, kind?, strict?)`). The biggest user-facing piece.
- Remaining Stage 4 closure: PLang `.test.goal` bodies need the builder
  to recognise `as <type> strict` as a goal-step syntax that maps to
  `variable.set.Type = <type-entity>`. The builder's natural-language →
  formal step path isn't this branch's scope.
