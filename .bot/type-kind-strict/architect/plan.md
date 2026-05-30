# Plan — Structured type values: `{name, kind, strict}`

> **Coder/test-designer: you own the final shape.** Every code sketch and signature in this plan and its topic docs is a *suggestion* to convey intent. If the real code wants a different shape, take it — and tell me what changed and why. The design decisions (the model, the canonicalisation rule, where validation lives, how types reach the LLM) are what's settled; the literal C# is not.

> **Base:** rebased onto `runtime2` after the singular-namespaces merge (`d96ec269f`). That merge already did two things this plan had reached for: it renamed the tree to singular (`type`/`format`/`module`/`variable`) and **promoted the type descriptor to an entity** — `app.type.@this` (`PLang/app/type/this.cs`) is now both doors (`data.Type` and `app.Type[name]`) and has *absorbed the catalog* (`Fields`/`Values`/`Kinds`/`Shape`/`Example`/…, lazily via `Promote()`). So descriptor↔catalog is already unified; what remains is to give that entity the `{name, kind, strict}` structure and resolve the kind-naming knot the merge left behind.

## Why

A PLang value's type is still keyed on a flat string — `"string"`, `"image/jpeg"`, `"text/markdown"` — and the meaning of that string is muddy: sometimes a primitive, sometimes a MIME family, sometimes a full MIME type. The `plang-types` work added the pieces (`Data.Kind` stamped at build, `number` advertising its kinds, `image` deriving its kind from the extension) and the singular-namespaces merge folded the catalog onto the type entity — but the model is still unfinished: the entity carries a flat `Value` not `{name, kind, strict}`, `Data.Kind` is a separate field, the entity exposes a family-`Kind` *and* a vocabulary-`Kinds` (two meanings, one word), and `variable.set`'s `as text` is hand-written prose.

This plan finishes it. A type becomes a structured value `{name, kind, strict}` — MIME-shaped, where `name` is the family or primitive (`text`, `number`, `image`), `kind` is the sub-format (`md`, `gif`, `int`), and `strict` says whether the kind is a requirement or a hint. `name` is runtime-independent (no C# leak on the PLang surface). The kind is a hint by default and is extrapolated from the file extension; `strict` turns it into a build-time requirement, but only for formats we can actually verify by sniffing bytes (a GIF), never for formats we can't (plain vs. markdown text — both are just text). And the type vocabulary the LLM sees collapses into one coherent surface that teaches what kinds exist and that the file extension sets the kind.

The trigger: Ingi is creating the `text` type now and wants `variable.set` to take a `type` instead of a `string`. `text` is the proving instance — once it lands as a real type with an extension-derived kind, `number`/`image`/`text` are all the same shape, and the muddy string is gone.

## The model

A `type` value (`app.type.@this`) carries three identity fields:

- **`name`** — the family or primitive. Canonical names: `text`, `number`, `bool`, `datetime`, `date`, `time`, `duration`, `guid`, `bytes`, `object`, `list`, `dict`, and the media/file families `image`, `video`, `audio`, … . Runtime-independent — never a CLR type. `string` is accepted on input and canonicalises to `text`; `int`/`long`/`decimal`/`double` are no longer top-level names — they are *kinds* of `number`.
- **`kind`** — the sub-format. A free string, optional. Default behaviour is a hint. Two ways a type knows its kinds: **advertised** (`number` declares a fixed list `int|long|decimal|double` — these aren't extensions) or **extension-derived** (`text`, `image`, `video`, `audio` have a `Build(value)` hook that pulls the kind from the file extension — `md`, `gif`, `mp4`). Canonical kind token = the file extension; `markdown`/`jpeg` are accepted and normalised to `md`/`jpg` at build.
- **`strict`** — bool, default false. False = the kind is a hint (stamp it, validate nothing). True = the kind is a requirement, enforced **only** for binary families that can be verified by sniffing bytes; for unverifiable families (`text`) strict is a no-op beyond "the kind name must be known."

This entity is already both the descriptor on every `Data` (`Data.Type`) and the value `app.Type[name]` returns. `Data.Kind` (still a separate field at `PLang/app/data/this.cs`) folds into `type.Kind` — one home. The wire form is unchanged: still the two flat keys `type` and `kind`, written by reading `Type.Value`→`type` (the name) and `Type.Kind`→`kind`. `ClrType` comes off the public `type` surface — name→CLR resolution stays internal to `app.type.list.@this`.

## Cross-cutting decisions

- **One home for the kind.** `Data.Kind` is removed as a sibling field; the `type` entity owns `name + kind + strict`. The wire still serialises two flat keys (`type`, `kind`) — the model collapses, the wire shape doesn't. Kills the holds-a-reference-and-a-flat-copy smell and the live bug where `variable.set` drops `Value.Kind` when minting. (`Data.Type` is now non-null via the `type.@this.Null` sentinel, so the fold has no null-Type edge case.)
- **`name` is runtime-independent.** `ClrType` is internal to the registry, not a public property on the PLang-visible entity. A PLang dev reading `%x.Type%` sees `{name, kind, strict}`, never a `System.Type`.
- **Resolve the three-way kind knot.** Today the entity exposes `Kind` (the *family*, via `App.Format.KindOf`) **and** `Kinds` (the advertised *vocabulary*), and there's also `App.Type.Kinds` (the build-hook *dispatcher*, `app.type.kind.@this`). After this plan: `name` is the family (so the family-`Kind` accessor goes away), `kind` is the subtype (folded from `Data.Kind`), `Kinds` stays as the vocabulary. The dispatcher `App.Type.Kinds` should be renamed (e.g. `App.Type.KindHooks`) so "Kind/Kinds" stops meaning three things.
- **kind canonicalisation is build-time, derived from the format registry.** Accept `md|markdown`, `jpg|jpeg`; normalise to the extension. The alias table falls out of the existing extension↔MIME map (`app.format.list.@this`) — no new hand-maintained data. Unknown free-string kinds pass through untouched.
- **Validation rides existing seams.** Kind derivation is already wired (`NormalizeParameterTypes` calls `App.Type.Kinds.Of(type, value)` → the `Build` hook). Strict validation goes into the per-action `ValidateBuild` seam (`build.validate`), which already skips `%var%` and defers those to runtime. No new build pass.
- **The LLM sees one type surface.** The universal type+kind vocabulary moves into the cached system prompt (generated from the catalog via `app.builder.type.@this`, replacing the hand-written list); the per-step user message keeps only step-specific domain/record types; the flat `Primitive types:` line is removed. `type` is taught as a constructor `type(name, kind?, strict?)` — name and kind emitted separately, never the `text/md` slash form.

## Stage index

| Stage | File | What it delivers |
|-------|------|------------------|
| 1 | [type value model](stage-1-type-value-model.md) | `app.type.@this` gains `Name`/`Kind`/`Strict`; fold in `Data.Kind`; internalise `ClrType`; drop the family-`Kind`; wire stays two-key. |
| 2 | [text type + name canonicalisation](stage-2-text-type-and-names.md) | Create `app/type/text/` (extension-derived kind); make `text` canonical for `string`; move `int/long/decimal/double` under `number`; drop them from the primitive list. |
| 3 | [kind derivation + canonicalisation](stage-3-kind-derivation.md) | Extension→kind for `text`; accept `md|markdown`/`jpg|jpeg`, normalise to extension at build; rename `App.Format.KindOf`→`FamilyOf` and the `App.Type.Kinds` dispatcher. |
| 4 | [variable.set + strict validation](stage-4-set-and-strict.md) | `variable.set.Type` becomes `type`; strict validation in `ValidateBuild` — sniffable families error on strict, warn on default, `%var%` deferred to runtime. |
| 5 | [LLM type representation](stage-5-llm-representation.md) | Cached vocabulary block with two render modes; drop the flat primitive line; generate the system-prompt type list from the catalog; teach `type(name, kind?, strict?)`. |

## Topic deep-dives

- [plan/type-value-model.md](plan/type-value-model.md) — the `{name, kind, strict}` structure on the promoted entity, the `Data.Kind` fold, `ClrType` internalisation, the wire shape, and the `IKindValidatable` seam for strict.
- [plan/kind-derivation-and-validation.md](plan/kind-derivation-and-validation.md) — how a kind is set (LLM intent vs. build derivation), the canonicalisation rule, and where strict is enforced.
- [plan/llm-type-representation.md](plan/llm-type-representation.md) — the restructured type surface, the two render modes, what moves to the cached system prompt, and the `type` constructor teaching.

## Test prep

- [plan/test-strategy.md](plan/test-strategy.md) — the narrative: scope, layer mapping (C# vs goal vs integration), the three integration cuts, and what the matrix picks up beneath them.
- [plan/test-coverage.md](plan/test-coverage.md) — the heavy reference: coverage matrix by topic, failure matrix (with the impossible-by-design negatives called out), and the inventory of new surfaces.

## Resolved during review

- **Numerics carry `{name: number, kind: int|long|decimal|double}`.** `- set %x% = 5` stamps `{number, int}` (Ingi). That's exactly what `number.Build` already computes (`5`→`int`, `3.14`→`decimal`, `1e5`→`double`). Consequence, forced by stage 2: since `int/long/decimal/double` are no longer top-level names, *every* numeric reads as `number` + kind — inferred values and action return types alike (`list.count → returns number(int)`). Inference must agree at both ends so `%x.Type%` never differs between build and runtime: the kind comes from `number.Build(value)` for a literal, or from the CLR numeric type for a declared return (`typeof(int)` → kind `int`).
