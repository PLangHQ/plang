# coder — v8 — codeanalyzer v1 response

Address all five findings. F1 blocking; F2–F5 fixed too (cheap + OBP-correct).

## F1 — strict kind validates at byte-materialization (Ingi's ruling)

The strict `(kind)` requirement rides **with the value** to its load seam via a
new value-side interface `app.data.IStrictKindEnforcer` (mirrors
`IBooleanResolvable`):

- `image` implements it: `RequireStrictKind(kind)` imprints; `CheckStrictKind()`
  sniffs loaded bytes (null = not loaded yet → defer). `BytesAsync()` enforces on
  load and throws `StrictKindMismatchException`. `ValidateKind` now reads a loaded
  `image.@this` (read its own bytes), not only raw `byte[]`.
- `variable.set` (generic, no image hardcode): after pinning the type, if strict
  + kind + `value is IStrictKindEnforcer`, imprint and check-now — an
  already-loaded value (read-lift, raw bytes) fails at the set; a lazy path-backed
  value defers to its own `BytesAsync`. Raw `byte[]` slots keep the existing
  `IKindValidatable` probe path.
- Tests: C# `Cut2.ReadLiftImagePngAsImageGifStrict_FailsAtSet` (loaded image
  fails at set) + `LazyPathHandleTests.BytesAsync_StrictKindMismatch_ThrowsAtLoad`
  (path-backed throws at load). PLang goals rewritten to the lazy contract:
  `SetAsImageGifStrictMismatch` asserts the set is clean (deferred);
  `SetAsImageGifStrictRuntimeVarMismatch` deleted — a read-lift through a variable
  comes back path-backed (defers, same as Mismatch) and the goal wouldn't build
  reliably (LLM step-count non-determinism); read-lift enforcement is C#-covered.

## F2 — drop the flat `kind` wire sibling
`Data.Kind` → `[JsonIgnore]` (was `[JsonPropertyName("kind")] [Out, Store]`). Kind
rides the wire inside the `type` entity only; the builder's `p.Kind = kind` folds
into `type.Kind`.

## F3 — `type.@this.Scheme` → `Context?.App.Type.Scheme` (null-guard).

## F4 — delete `text/this.Build.cs` (the spelling-kind hook).
text's kind is a producer/Format concern, never a literal's spelling, so the hook
shouldn't exist. Removing it makes the `!= "text"` gate in `set.cs` unnecessary
(removed) and deletes the now-invalid `TextBuildHookTests`.

## F5 — removed dead `CanonicaliseKind` fast-path; `BuilderNames` initialises
inline (dropped the `BuildBuilderNames` wrapper).

## Verification
C# 3815/3815. PLang 262/262 (0 fail, 0 stale).
