# Test strategy

> **Test-designer: you own the final test shapes.** The cuts, layers, and matrix rows below are the architect's view of what must be proven and where. If a behaviour wants a different layer or a row is redundant, change it — and say what changed. The heavy per-behaviour reference is in [test-coverage.md](test-coverage.md); read it alongside this.

## Scope

The three **integration cuts** below are the contract for end-to-end behaviour — they prove the build→runtime path and the LLM prompt for the whole feature. Per-topic C# unit tests and per-surface `.goal` tests sit *beneath* them in [test-coverage.md](test-coverage.md): they pin internal pieces (type parse, canonicalisation, byte-sniff, rendering) and the developer-facing `as` surface that the cuts exercise only at the happy-path level.

## Test layer mapping

The rule:

- **C# TUnit pins the internal, deterministic, LLM-free machinery** — the `type` value (`Name`/`Kind`/`Strict` parse, slash tolerance, `string`→`text`), kind canonicalisation (`md|markdown`→`md`, unknown passthrough), `text.Build` extension extraction, `image.ValidateKind` byte-sniff, the `Data.Kind` fold (no stored field), the wire two-key shape, the primitives table, and `TypeSchemas`' two render modes.
- **PLang `.goal` pins the developer-facing surface** — `set %x% = "a" as text` produces a `text`-typed variable; `as image/gif` lands `kind=gif`; `as text/markdown` normalises to `kind=md`; a strict mismatch fails the build; a bare `set` keeps inference. These run through the real build + runtime, so they prove the surface a PLang dev actually touches.
- **Integration cuts pin the contract** — the full build→runtime round-trip, the strict-error path, and the LLM representation.

Per-behaviour layer assignment is in the coverage matrix; this is the rule, not the enumeration.

## Integration cuts

**Cut 1 — a typed `set` round-trips its kind.** Build a goal containing `set %doc% = "readme.md" as text`, run it, and assert the resulting variable's type is `{name: "text", kind: "md"}` — the kind derived from the extension at build, carried through the mint, and visible on the wire / via navigation (`%doc.Type.Name%` = `text`, `%doc.Type.Kind%` = `md`). This is the spine of the feature: structured type (stage 1), `text` canonical (stage 2), extension→kind (stage 3), mint-carries-kind (stage 4) all firing together. It is also the regression guard for the dropped-kind bug.

**Cut 2 — strict mismatch fails at the right layer.** Build `set %img% = "photo.png" as image/gif strict` (literal value, sniffable family, deliberate mismatch) and assert a **build** error, not a runtime one — proving strict runs in `ValidateBuild` via `image.ValidateKind`. Pair it with the `%var%` variant (`set %img% = %upload% as image/gif strict`) which builds clean and fails at **runtime** with a typed error — proving the build-vs-runtime split. And `set %img% = "real.gif" as image/gif strict` builds clean (match).

**Cut 3 — the LLM sees one unified vocabulary.** Force a fresh compile (`plang '--build={"files":[...],"cache":false}'`) of a goal referencing `text`/`number`/`image`, read the new trace under `.build/traces/<id>/`, and assert: the cached system prompt carries the generated type vocabulary with `number — kinds: int | long | decimal | double` and `text — kind = extension`; the flat `Primitive types:` line is gone from the per-step user message; and the `type` entry renders for `variable.set`. Proves stage 5 against the real prompt — the same method this plan was researched with.

## What's not covered by these cuts

The matrix picks up beneath the cuts:

- **Per-type C# units** — `type` parse/slash-tolerance, every canonicalisation alias (`markdown`/`jpeg`/unknown/shared-subtype), `text.Build` edge cases (no extension, `%var%`, query string), `image.ValidateKind` per-format.
- **Negative paths** — unknown type name, value-not-convertible, malformed multi-slash type string, and the *impossible-by-design* ones (strict byte-check on `text` — there's no probe, so no failure to assert).
- **The `Data.Kind` fold** — no stored field; `Data.Kind` reads `Type.Kind`; wire still two keys.
- **The primitives table** — `BuilderNames` includes `text`, excludes `string`/`int`/`long`/`decimal`/`double`.
- **The formats rename** — `FamilyOf` (was `KindOf`) still maps `image/jpeg`→`image`.
- **The runtime strict issuer** — cut 2 proves build strict and one runtime case; the matrix covers the runtime path per family.
