# Stage 8: Build-time type flow — fundamental vocabulary, literal/kind rules, prompt scoping

> **Coder: you own the final shape.** The settled model is in [plan/build-time-type-flow.md](plan/build-time-type-flow.md) — the spine, the two type categories, the four rules. This stage maps that model to where the current code (after stages 6–7) diverges, and what to change. The decisions are settled; the code shape is yours.

**Goal:** Make the builder match the agreed type-flow model: a bare literal is its value-shape type with no kind, the per-step prompt carries only what the step needs (not the full catalog), and `image/video/audio/path` are first-class fundamentals.
**Scope:** Included — the `variable.set` literal rule + its `Build()`, the `CompileUser.llm` teaching, the per-step type-info assembly (`builder.types`/the Schema `Build()`), and defining the fundamental vocabulary in two categories. Excluded — the `hash` *value-type* mechanics and relocation (stage 7 rev 2 owns those); runtime serialization.
**Dependencies:** Stages 1–7. The cross-step memory (`goal.getTypes → %stepVarTypes%`) already exists and is the spine — this stage trims and corrects what flows through it, it does not rebuild it.

## Where the code diverges from the model

Four gaps, each tied to a rule in the model doc.

### 1. Bare literal is promoted by spelling → must become `text`

Today the teaching (`CompileUser.llm`) instructs the LLM to map a string literal by its extension: `"readme.md" → {text, md}` and a media-extension literal → the media type (`{image, jpg}`). Both violate **rule 1/2** — the value is a literal string, not content.

**Fix:** bare string literal → `{text}`, no kind. Make it **deterministic** via `variable.set.Build()` (per Ingi): when no explicit type was declared, force the value-shape type (string → text); parse the value into a kind **only when `name != text`** (a reference fundamental the developer named — `as image` → parse path → `{image, jpg}`). Build does *not* read extensions for `text`. Update the `CompileUser.llm` examples to match (drop the `readme.md → {text,md}` and media-extension-literal rules; a bare literal is its value shape, kind only via `as`).

### 2. The full catalog is dumped every step → scope the prompt

Today `app/builder/type/this.cs::Build()` assembles the `Kinds` table from **all** known types (`BuildTypeEntries(null)`) — which is why `hash`'s algorithms leak into every step's prompt. Violates **rule 3**.

**Fix:** the per-step type info carries only: the **small fundamental vocabulary** (below), the types the **current step's actions** reference (the `actions=` filter already narrows the `Types` list — extend the same scoping to the `Kinds`/names so it isn't full-catalog), and the **runtime-fed in-scope variable types** (`%stepVarTypes%`, already present). Drop the all-types `Kinds` pass. This **subsumes stage 7's "keep hash out of the emit table"** — once the prompt is fundamentals-only, `hash` (not a fundamental) is out automatically, while staying fully registered for `getTypes`/validation.

### 3. The fundamental vocabulary isn't defined as such → define it, in two categories

Today the always-on "primitive" list is the primitive aliases (`text/number/bool/...`); `image/video/audio/path` are *not* first-class there — they surface only as format families in the `Kinds` table. The model makes them **fundamental**.

**Fix:** define the fundamental set explicitly, in the two categories from the model:
- **Inline fundamentals:** `text, number, bool, object, list, dict, datetime, date, time, duration, guid`.
- **Reference fundamentals:** `image, video, audio, path` (`bytes` parked with them).

Both always-on in the prompt vocabulary. This is what grounds a developer's `as image` (rule: the LLM recognises it because it's in the set, not by prose-guessing). The kind-vocabulary the LLM may emit narrows to these fundamentals' kinds (text/image/… extensions, number precisions) — not every registered type's `Kinds`.

### 4. (Confirm, don't rebuild) the runtime is the cross-step memory

`goal.getTypes` walking prior steps → `%stepVarTypes%` → `CompileUser.llm` is the spine and it's correct. This stage only ensures the *right* type flows through it: once `crypto.hash` returns `hash` (stage 7 rev 2), `%bla% (hash)` appears here for free. No change to the mechanism — just verify a producing action's refined `Build()` type (rule 4) is what lands in the map, not the static floor.

## Deliverables

- `variable.set` literal rule (`Build()`): bare literal → value-shape type, no kind; parse value→kind only when `name != text`.
- `CompileUser.llm`: teaching matches rules 1–2 (no spelling-based promotion; kind only via `as` or a producing action).
- Per-step type info scoped to fundamentals + step-action types + in-scope types; full-catalog `Kinds` dump removed (subsumes stage 7's emit-table fix).
- Fundamental vocabulary defined in two categories, `image/video/audio/path` first-class and always-on.
- Tests: `set %x% = "file.jpg"` → `{text}` (no image, no kind); `set %x% = "file.jpg" as image` → `{image, jpg}`; a later step sees `%var% (hash)` from a prior `crypto.hash`; a step's prompt does **not** contain `hash`'s kinds.

## Note on ordering vs stage 7

Stage 7 rev 2 (return `Data<hash.@this>`, relocate to the crypto module) and this stage are complementary: stage 7 makes `%bla%`'s type *be* `hash`; stage 8 makes the builder *show and scope* it correctly. Stage 8's prompt-scoping (gap 2) replaces stage 7's narrower "pull hash out of the emit table" — so when both land, do the scoping here and drop that line from stage 7.
