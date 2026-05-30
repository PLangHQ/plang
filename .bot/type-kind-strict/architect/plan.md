# Plan — Structured type values: `{name, kind, strict}`

> **Coder/test-designer: you own the final shape.** Every code sketch and signature in this plan and its topic docs is a *suggestion* to convey intent. If the real code wants a different shape, take it — and tell me what changed and why. The design decisions (the model, the canonicalisation rule, where validation lives, how types reach the LLM) are what's settled; the literal C# is not.

## Why

A PLang value's type is currently a flat string — `"string"`, `"image/jpeg"`, `"text/markdown"` — and the meaning of that string is muddy: sometimes a primitive, sometimes a MIME family, sometimes a full MIME type. The `plang-types` merge started fixing this (it added a `Data.Kind` field stamped at build, the `number` type that advertises its kinds, the `image` type whose kind is extension-derived) but stopped half-way: the kind lives in two places under two meanings, `variable.set`'s `as text` is taught as hand-written prose, and the LLM sees the type vocabulary in three places that disagree with each other (`int` is presented both as a top-level primitive and as a kind of `number`).

This plan finishes the model. A type becomes a structured value `{name, kind, strict}` — MIME-shaped, where `name` is the family or primitive (`text`, `number`, `image`), `kind` is the sub-format (`md`, `gif`, `int`), and `strict` says whether the kind is a requirement or a hint. `name` is runtime-independent (no C# leak on the PLang surface). The kind is a hint by default and is extrapolated from the file extension; `strict` turns it into a build-time requirement, but only for formats we can actually verify by sniffing bytes (a GIF), never for formats we can't (plain vs. markdown text — both are just text). And the type vocabulary the LLM sees collapses into one coherent surface that teaches what kinds exist and that the file extension sets the kind.

The trigger: Ingi is creating the `text` type now and wants `variable.set` to take a `type` instead of a `string`. `text` is the proving instance — once it lands as a real type with an extension-derived kind, `number`/`image`/`text` are all the same shape, and the muddy string is gone.

## The model

A `type` value carries three fields:

- **`name`** — the family or primitive. Canonical names: `text`, `number`, `bool`, `datetime`, `date`, `time`, `duration`, `guid`, `bytes`, `object`, `list`, `dict`, and the media/file families `image`, `video`, `audio`, … . Runtime-independent — never a CLR type. `string` is accepted on input and canonicalises to `text`; `int`/`long`/`decimal`/`double` are no longer top-level names — they are *kinds* of `number`.
- **`kind`** — the sub-format. A free string, optional. Default behaviour is a hint. Two ways a type knows its kinds: **advertised** (`number` declares a fixed list `int|long|decimal|double` — these aren't extensions) or **extension-derived** (`text`, `image`, `video`, `audio` have a `Build(value)` hook that pulls the kind from the file extension — `md`, `gif`, `mp4`). Canonical kind token = the file extension; `markdown`/`jpeg` are accepted and normalised to `md`/`jpg` at build.
- **`strict`** — bool, default false. False = the kind is a hint (stamp it, validate nothing). True = the kind is a requirement, enforced **only** for binary families that can be verified by sniffing bytes; for unverifiable families (`text`) strict is a no-op beyond "the kind name must be known."

This `type` value is both the descriptor on every `Data` (`Data.Type`) and the value the LLM constructs for a `type`-typed parameter (`variable.set.Type`, and any future action that annotates a type). `Data.Kind` (the separate field plang-types added) folds into `type.Kind` — one home. The wire form is unchanged: still two flat keys `type` and `kind`, written by reading `Type.Name` and `Type.Kind`. `ClrType` comes off the public `type` surface — name→CLR resolution stays internal to `app.types.@this`.

## Cross-cutting decisions

- **One home for the kind.** `Data.Kind` is removed as a sibling field; the `type` value owns `name + kind + strict`. The wire still serialises two flat keys (`type`, `kind`) — the object collapses, the wire shape doesn't. This kills the holds-a-reference-and-a-flat-copy smell and the live bug where `variable.set` drops `Value.Kind` when minting.
- **`name` is runtime-independent.** `ClrType` is internal to the registry, not a property on the PLang-visible `type`. A PLang dev reading `%x.Type%` sees `{name, kind, strict}`, never a `System.Type`.
- **kind canonicalisation is build-time, derived from the formats registry.** Accept `md|markdown`, `jpg|jpeg`; normalise to the extension. The alias table falls out of the existing extension↔MIME map — no new hand-maintained data. Unknown free-string kinds pass through untouched.
- **Validation rides existing seams.** Kind derivation is already wired (`NormalizeParameterTypes` calls `Kinds.Of(type, value)` → the `Build` hook). Strict validation goes into the per-action `ValidateBuild` seam (`build.validate`), which already skips `%var%` and defers those to runtime. No new build pass.
- **The LLM sees one type surface.** The universal type+kind vocabulary moves into the cached system prompt (generated from the catalog, replacing the hand-written list); the per-step user message keeps only step-specific domain/record types; the flat `Primitive types:` line is removed. `type` is taught as a constructor `type(name, kind?, strict?)` — name and kind emitted separately, never the `text/md` slash form.

## Stage index

| Stage | File | What it delivers |
|-------|------|------------------|
| 1 | [type value model](stage-1-type-value-model.md) | `app.data.type` becomes `{Name, Kind, Strict}`; fold in `Data.Kind`; internalise `ClrType`; wire stays two-key. |
| 2 | [text type + name canonicalisation](stage-2-text-type-and-names.md) | Create the `text` type (extension-derived kind); make `text` canonical for `string`; move `int/long/decimal/double` under `number`; drop them from the primitive list. |
| 3 | [kind derivation + canonicalisation](stage-3-kind-derivation.md) | Extension→kind for `text`; accept `md|markdown`/`jpg|jpeg`, normalise to extension at build; rename the formats registry's "kind"→"name" (family). |
| 4 | [variable.set + strict validation](stage-4-set-and-strict.md) | `variable.set.Type` becomes `type`; strict validation in `build.validate` — sniffable families error on strict, warn on default, `%var%` deferred to runtime. |
| 5 | [LLM type representation](stage-5-llm-representation.md) | Cached vocabulary block with two render modes; drop the flat primitive line; generate the system-prompt type list from the catalog; teach `type(name, kind?, strict?)`. |

## Topic deep-dives

- [plan/type-value-model.md](plan/type-value-model.md) — the `{name, kind, strict}` structure, the `Data.Kind` fold, `ClrType` internalisation, the wire shape, and the `IKindValidatable` seam for strict.
- [plan/kind-derivation-and-validation.md](plan/kind-derivation-and-validation.md) — how a kind is set (LLM intent vs. build derivation), the canonicalisation rule, and where strict is enforced.
- [plan/llm-type-representation.md](plan/llm-type-representation.md) — the restructured type surface, the two render modes, what moves to the cached system prompt, and the `type` constructor teaching.

## Open / deferred

- **Number's kind as `type.kind`.** `number` advertises `int|long|decimal|double` via a static `Kinds` list, and internally has `NumberKind`. Whether `Data.Type.Kind` for a number reads back the `NumberKind` (so `%n.Type.Kind%` = `"int"`) is a consistency question — pulls `number` fully into the same model. Flagged, not yet decided.
- **Test strategy.** `plan/test-strategy.md` + `plan/test-coverage.md` come after this design is reviewed — premature to pin coverage before the shape is confirmed.
