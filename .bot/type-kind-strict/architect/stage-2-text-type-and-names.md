# Stage 2: The `text` type and name canonicalisation

**Goal:** Make `text` a real type with an extension-derived kind, make `text` the canonical name for `string`, and move `int`/`long`/`decimal`/`double` under `number` as kinds so they stop being top-level primitive names.
**Scope:** Included — a new `app/types/text/` type, the primitives table (`app/types/primitives/this.cs`), `GetBuilderTypeNames`/`BuilderNames`. Excluded — kind canonicalisation aliases (stage 3), the LLM vocabulary block (stage 5).
**Deliverables:** `text` type (`Build(value)` extension→kind hook, no static `Kinds`, `Shape="string"`, description teaching kind-from-extension); `Canonical[typeof(string)] = "text"`; `string` kept as an accepted alias; `int/long/decimal/double` removed from the builder primitive names (surfaced only as `number` kinds).
**Dependencies:** Stage 1 (the `type` value model).

## Design

Ingi is creating `text` now — this stage is the design it should match.

- **`text` mirrors `image`, text-backed instead of bytes-backed.** It has a `static string? Build(object?)` hook that extracts the file extension as the kind (`report.md` → `md`), exactly as `image.Build` does. It declares **no** static `Kinds` list — kind is open (a free string when there's no extension). `Shape => "string"`. Its description/notes carry the kind-from-extension teaching that will render in the LLM vocabulary (stage 5).
- **`text` becomes canonical for `string`.** In `primitives.@this`, set `Canonical[typeof(string)] = "text"`. `Aliases` already has both `"string"` and `"text"` → `typeof(string)`, so input `string` still resolves and renders back as `text`. Make `BuilderNames` pick `text` (it currently dedups to the first alias key, `string`; reorder so `text` precedes `string`, or derive `BuilderNames` from `Canonical`). This is global: every string value's `Type.Name` becomes `text` on the wire, in navigation, and in the catalog. Expect tests asserting `type=="string"` to need updating — that churn is intended, not a regression.
- **`int/long/decimal/double` leave the primitive name list.** They are kinds of `number` (which already advertises `Kinds = [int, long, decimal, double]`). Drop them from `GetBuilderTypeNames`/`BuilderNames` so the LLM sees `number` (with kinds) rather than the primitives competing with it. Keep their `Aliases` entries (so `int` still resolves to a CLR type for conversion) — only their *top-level catalog presence* goes away. `number` carries them as kinds in the vocabulary block.
- The convergence to confirm: a plain string is `{name: text}`; a markdown string is `{name: text, kind: md}`; a `.md` file's contents are `{name: text, kind: md}`. The `text` primitive and the `text/*` MIME family are the same name — that's the point.
