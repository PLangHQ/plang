# Architect — type-kind-strict

## 2026-05-30 — initial design: structured type values `{name, kind, strict}`

Designed the finish of the type-value model the `plang-types` merge started. A PLang value's type becomes a structured `{name, kind, strict}` instead of a flat string: `name` is the family/primitive (`text`, `number`, `image`), `kind` is the sub-format (`md`, `gif`, `int`), `strict` says whether the kind is a requirement or a hint. Triggered by Ingi creating the `text` type and wanting `variable.set` to take a `type` instead of a `string`.

Settled across the conversation, grounded by reading the type system and capturing a fresh compile trace from `Tests/Simple`:

- `app.data.type` gains `Name`/`Kind`/`Strict`; the separate `Data.Kind` field folds in (one home); `ClrType` comes off the public surface (name→CLR stays internal to the registry). Wire stays two flat keys.
- `text` is a real type mirroring `image` — `Build(value)` extension→kind hook, no static `Kinds` (kind open), `Shape="string"`. `text` becomes canonical for `string`, globally. `int/long/decimal/double` move under `number` as kinds and leave the primitive list.
- Two ways a kind is known: advertised (`number`) vs extension-derived (`text`/`image`/`video`/`audio`). Canonical kind = the extension; `md|markdown`/`jpg|jpeg` accepted and normalised at build via an alias table derived from the formats registry.
- Strict is enforced only for verifiable binary families (`image` sniffs bytes), in the existing `ValidateBuild` seam — error on strict mismatch, warn/nothing on default, `%var%` deferred to runtime. Per-type logic behind an `IKindValidatable` marker, never a switch in `variable.set`/`build.validate`.
- The LLM type info collapses to one surface: the universal vocabulary moves into the cached system prompt (generated from the catalog, two render modes), the per-step block keeps only step-specific domain types, the flat `Primitive types:` line is dropped, and `type` is taught as `type(name, kind?, strict?)`.

One cleanup rides along: the formats registry calls the family "kind" — under this vocabulary that's the *name*; rename so the codebase stops using "kind" for two things.

Branch created off `runtime2` (d782fe2b5) — clean base with the whole type system; `prevars-in-pr` only carried unbuilt design docs, so it was not used as the base.

Status: design written, nothing built. Awaiting Ingi's review comments. Test strategy (`plan/test-strategy.md` + `plan/test-coverage.md`) deferred until the design is confirmed.

Stage status:
| Stage | File | Status |
|-------|------|--------|
| 1 | [type value model](stage-1-type-value-model.md) | pending |
| 2 | [text type + name canonicalisation](stage-2-text-type-and-names.md) | pending |
| 3 | [kind derivation + canonicalisation](stage-3-kind-derivation.md) | pending |
| 4 | [variable.set + strict validation](stage-4-set-and-strict.md) | pending |
| 5 | [LLM type representation](stage-5-llm-representation.md) | pending |
