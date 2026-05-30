# Architect — type-kind-strict

## 2026-05-30 — producers gap found; stages 6–7 carved (file.read + http + hash)

Coder landed stages 1–5 (entity `{Name,Kind,Strict}`, `text` canonical for `string`, kind derivation, `variable.set` takes a `type`, LLM rendering) and unified the wire to **one structured `type` field** `{name, kind?, strict?}` (commit `42b8430d6`) — the old "two flat keys" wording in this plan is superseded by that.

Working through the read path with Ingi, found the gap: stages 1–5 fixed the type *model* and the *consumer* (`variable.set`), but the *producers* were never migrated. `file.read` stamps two different wrong answers for the same file — runtime `FromMime("text/markdown")` (full MIME as the name, no kind, because `FromMime` bypasses `Create` so the slash never splits) and build a bare extension `"md"`. `http` has the identical split. Numbers and plain CLR values are fine (the `Data.Type` getter auto-derives the name via `Canonical` and stamps the numeric kind itself).

Key reframe for Ingi: "scan the codebase for everything that returns string → text" is **not** the work. The CLR carrier stays `string`; only the PLang *name* changes, and that's one boundary — `Canonical[typeof(string)] = "text"` (already landed). Producers needing a manual `{name,kind}` stamp are only the ones whose value carries a sub-format the CLR type can't express: file/network reads (extension/content-type) and hashes (algorithm).

Two stages carved:
- **Stage 6** — one shared `(extension|mime) → type{name,kind}` derivation that both `file.read.Build()` and `ReadText` call (so build==runtime, killing the drift); migrate `file.read` (incl. image-lift) and `http`; non-string reads return raw text and materialize lazily via `SetValue`/`ConvertValue` (Ingi's todo, scoped to *every* non-string conversion).
- **Stage 7** — `hash` as a first-class type, `kind` = the algorithm (`sha256`/`keccak256`), so `crypto.verify` reads the algorithm off the value instead of taking it as a loose parameter. Settled `hash` over `checksum` (these are cryptographic digests in the `crypto`/signing path; checksum connotes non-crypto CRC/Adler). Different mechanism from stage 6 (advertised kind, not extension-derived) → separate stage.

Also captured a todo: lazy structured-parse on file read (`Documentation/Runtime2/todos.md`, 2026-05-30).

Open for Ingi: confirm the 6/7 split and the `hash` name before this goes to coder.

Stage status:
| Stage | File | Status |
|-------|------|--------|
| 1–5 | (see plan index) | complete (coder v1–v5) |
| 6 | [structured type at producers](stage-6-structured-type-producers.md) | pending |
| 7 | [the `hash` type](stage-7-hash-type.md) | pending |

## 2026-05-30 — rebased onto runtime2 (singular-namespaces + type-entity promotion)

Merged latest `runtime2` (`d96ec269f`) into the branch — clean (my work was `.bot/`-only). That merge brought the singular-namespaces rename (`types`→`type`, `formats`→`format`, `modules`→`module`, `variables`→`variable`) and, more importantly, **promoted the type descriptor to an entity**: `app.type.@this` (`PLang/app/type/this.cs`) is now both doors (`data.Type` and `app.Type[name]`) and has absorbed the old `app.builder.type.Entry` catalog (`Fields`/`Values`/`Kinds`/`Shape`/… via lazy `Promote()`). So the descriptor↔catalog unification this plan reached for is already done — differently.

Reconciled the whole plan onto the new base: rebased every path/namespace, reframed Stage 1 (now adds `Name`/`Kind`/`Strict` to the promoted entity rather than restructuring a flat wrapper), and adjusted Stage 5 (catalog folded; renderer is `app.builder.type.@this`). The design is unchanged in substance — all five stages still needed (entity still keyed on flat `Value`, `Data.Kind` still separate, `variable.set.Type` still `string`, primitive `Canonical` still `string`). The merge sharpened the kind-naming knot into a three-way one (`type.Kind` family / `type.Kinds` vocabulary / `Data.Kind` subtype, plus the `App.Type.Kinds` dispatcher), now resolved in the plan: `name`=family, `kind`=subtype, `Kinds`=vocabulary, dispatcher renamed `KindHooks`.

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

Status: design approved by Ingi with no comments. Five stage files carved and the test-designer prep written (`plan/test-strategy.md` + `plan/test-coverage.md`). Nothing built yet — ready to hand to test-designer / coder.

Stage status:
| Stage | File | Status |
|-------|------|--------|
| 1 | [type value model](stage-1-type-value-model.md) | pending |
| 2 | [text type + name canonicalisation](stage-2-text-type-and-names.md) | pending |
| 3 | [kind derivation + canonicalisation](stage-3-kind-derivation.md) | pending |
| 4 | [variable.set + strict validation](stage-4-set-and-strict.md) | pending |
| 5 | [LLM type representation](stage-5-llm-representation.md) | pending |
