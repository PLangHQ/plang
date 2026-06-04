# Architect — type-kind-strict

## 2026-05-31 — stage 9: reference fundamentals are lazy path-handles at runtime

Ingi walked the runtime of `- set %x% = "file.jpg" as image`: mint an `image`, set `.Path = "file.jpg"`, return it — **no file read**; content loads only when needed (a later step touching width/pixels). Carved **stage 9** for it.

Two real divergences found: (1) `image`'s constructor requires bytes (`Path` is just provenance) — needs path-backed lazy construction where `Bytes` loads from `.Path` on first access; (2) `variable.set` today mints a plain `Data<string>` annotated image for `as image` (a string-typed-image), not an actual image with `.Path` — that carve-out is what stage 9 replaces. The wrinkle I flagged for coder: lazy load from a path is **async I/O through the auth gate**, but `Bytes`/`Width`/`Height` are sync getters — so the path-backed content surface must be async (no sync-over-async), same reasoning as `IBooleanResolvable` making conditions async. Errors (missing file, bad decode) surface late at first access (consistent with lazy); cheap path-string validation can still fail at set.

Mutation/save (`set width to 200` → divergence from the backing file, copy-on-write, origin-vs-destination path) **explicitly parked** per Ingi — captured as a follow-up in stage 9, not designed.

Pushed `20f58d979` earlier (stages 7 rev2, 8, model doc). Stage 9 is local/unpushed at session end.

Stage status:
| Stage | File | Status |
|-------|------|--------|
| 1–5 | (see plan index) | complete (coder v1–v5) |
| 6 | [structured type at producers](stage-6-structured-type-producers.md) | done by coder, reviewed clean |
| 7 | [the `hash` type](stage-7-hash-type.md) | rev 2 written — needs redo |
| 8 | [type flow + vocabulary](stage-8-type-flow-and-vocabulary.md) | pending |
| 9 | [lazy reference handles](stage-9-lazy-reference-handles.md) | pending (unpushed) |

## 2026-05-31 — settled the build-time type-flow model; stage 8 carved

Long design conversation with Ingi that pulled up from C# to the flow. Settled model written to [plan/build-time-type-flow.md](plan/build-time-type-flow.md). Key things that landed:

- **The spine:** each step compiles as an independent LLM call with no memory of prior steps. The builder *runtime* is the cross-step memory — it walks built steps and feeds `%bla% (hash)` into the next step's prompt (`goal.getTypes → %stepVarTypes%`). So the prompt is rebuilt per step and should carry only what that step needs.
- **Two categories of fundamental type**, by one question — can you write the value inline, or only a reference to it? Inline: `text/number/bool/object/list/dict/datetime/...`. Reference: `image/video/audio/path` (you write a path, never the bytes). Ingi's realization ("you can write `true` but not image data, only the path") is the clean axis. Both are fundamental — PLang is domain-elevated, so media+path are first-class, unlike C#.
- **Four rules:** (1) kind only from explicit `as` or a producing action's `Build()` reading real content — never from a bare literal's spelling; (2) bare literal → value-shape type, no kind (`set "file.jpg"` → `text`, NOT image — kills the spelling-promotion magic); (3) per-step prompt = small vocabulary + step-action types + in-scope types, never the full catalog; (4) types enter on action returns (refinable by `Build()`), never developer-declared for result types like `hash`.

Carved **stage 8** to map this to where the code diverges: the `variable.set` literal rule + `Build()`, the `CompileUser.llm` teaching (drop spelling-promotion), per-step prompt scoping (replaces stage 7's narrower hash-out-of-emit-table), and defining the fundamental vocabulary in two categories with `image/video/audio/path` first-class. Stage 8's scoping subsumes part of stage 7; noted in both files.

Nothing pushed (Ingi holding pushes). Stages 6 (clean), 7 rev 2 (hash value-type + relocation), 8 (type flow) are the open work.

Stage status:
| Stage | File | Status |
|-------|------|--------|
| 1–5 | (see plan index) | complete (coder v1–v5) |
| 6 | [structured type at producers](stage-6-structured-type-producers.md) | done by coder, reviewed clean |
| 7 | [the `hash` type](stage-7-hash-type.md) | rev 2 written — needs redo (return `hash.@this`, relocate) |
| 8 | [type flow + vocabulary](stage-8-type-flow-and-vocabulary.md) | pending |

## 2026-05-31 — reviewed coder's stages 6–7; stage-7 rewritten (rev 2)

Coder shipped both stages (commit `21e887a3d`/`62a23c4e7`, green 3810/263). **Stage 6 is clean** — shared `TypeFromMime`/`TypeFromExtension` derivation, `file.read` + `http` build==runtime, correct `{name,kind}`; lazy materialization + http runtime-body stamp deferred per Ingi. Ship it.

**Stage 7 had one root defect with four faces, plus the placement error I introduced.** Coder followed my rev-1 doc and (a) put `hash` in `app/type/` (builtin) and (b) had `crypto.hash` return `Data<byte[]>` stamped `type=hash`. The `byte[]` return is the single root: it made the new serializer dead (Normalize dispatches by value CLR type → `bytes`, never `hash`), broke `verify %data% against %h%` in the real flow (`byte[]`→string has no base64 path → `FromBase64` throws; the test hid it by manually base64-encoding), and mismatched ClrType.

Ingi's framing closed it: the point of `hash` is **build-time type flow** — `hash %ble% write to %bla%` should make the builder show `%bla% (hash)` when compiling the next `verify %bla%`. Traced the chain: `crypto.hash.Run()` return type → `goal.getTypes.DetermineReturnType` → `chainReturnType` → `variable.set %!data%` → `%stepVarTypes%` → `CompileUser.llm`. Today the return is `Data<byte[]>` → builder shows `%bla% (bytes)`.

So **one change is the spine**: `crypto.hash` returns `Data<hash.@this>`. It delivers the `%bla% (hash)` annotation *and* fixes all three runtime faces at once. Rev-2 stage-7 written around that, plus: relocate `hash` → `app/module/crypto/type/` (confirm registry still resolves it via the return signature like `http.response`), wire read-back via `type.Convert` reading `Kind`, and replace the dishonest verify test with a real round-trip. Verify *split* (encoding+equality on the type, recompute in crypto) was correct — kept.

Open thread, deferred: the LLM **emit** kind-vocab table now lists hash's algorithms, but hash is produced-not-emitted — result-types leaking into the emit vocabulary. Distinct from the (wanted) variables-in-scope annotation.

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
