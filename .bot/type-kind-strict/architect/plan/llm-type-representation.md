# Topic — restructuring the type information sent to the LLM

This is the part Ingi asked to get right. Grounded in the actual compile prompt captured from a fresh build (2026-05-30), with paths updated for the singular-namespaces merge.

## Today — three surfaces that disagree

Captured from a real `--build` of `Tests/Simple`:

1. **System prompt** (`os/system/builder/llm/Compile.llm`, stable for caching) — a hand-written valid-type list: `string, int, long, float, double, decimal, bool, datetime, timespan, guid, json, list, dict, bytes, object, ...`.
2. **Per-step user message** (`os/system/builder/llm/CompileUser.llm`) — a flat line repeated in *every* step's message: `Primitive types: string, int, long, float, double, decimal, bool, datetime, date, time, duration, guid, byte, bytes, list, array, dictionary, object, json`. Says `string`, not `text`. Lists `int/long/float/double/decimal` as separate primitives. **`number` is not in it.**
3. **Per-step catalog block** (`CompileUser.llm`, scoped to the step's actions) — `Catalog types referenced by this step's actions:` rendered by `TypeSchemas` on `app.builder.type.@this` (`PLang/app/builder/type/this.cs`). For the `set` step it rendered `object: string`. This is where `(kinds: …)` shows; `number` would render `number: string (kinds: int | long | decimal | double)` when a step references it.

The three disagree: `int` appears as a primitive in (1) and (2) *and* as a kind of `number` in (3). `string` in (1)/(2) vs the `text` we're moving to. The `as text` mechanism isn't represented as a type — it's three paragraphs of prose in `variable.set`'s Notes mapping "as text" → `Type="string"`.

Note what the merge already did: the catalog `Entry` struct dissolved into `app.type.@this`, so `TypeSchemas` now renders from `type.@this` entities (their folded `Fields`/`Values`/`Kinds`/`Shape`), and `data.Type` / `app.Type[name]` are the same shape. The descriptor↔catalog split is gone — but the *prompt* still carries the three disagreeing surfaces above.

## Target — one surface, two render modes

Collapse to a single type vocabulary, split by *what varies per build* rather than primitive-vs-domain:

- **Cached system prompt — the universal vocabulary.** The core type names + their kinds are goal-invariant, so they belong in the stable system prompt (provider-cached), generated from the catalog (`app.builder.type.@this` / `app.type.list.@this.BuildTypeEntries`), replacing the hand-written list in `Compile.llm`. This is the "what kinds exist + how to set a kind" teaching, written once. Generating it means it can't drift from the per-step block again.
- **Per-step user message — only step-specific types.** Keep the scoped block, but it now carries *only* the domain/record/enum types this step's actions reference (`path`, `llmmessage`, enums, records). The flat `Primitive types:` line is **removed** — its content moved into the cached vocabulary.

### Two render modes for kinds

`TypeSchemas` renders from `type.@this` entities; it gains a second mode, keyed off signals already on the entity:

- **Advertised** — the entity's `Kinds` vocabulary is populated (only `number` today). Render `number — kinds: int | long | decimal | double`. A closed vocabulary the LLM picks from. These aren't extensions.
- **Extension-derived** — the type has a `Build(value)` hook but no `Kinds` (`image`, and `text` once built). Render `image — kind = file extension (jpg, png, gif, …)`. Don't enumerate exhaustively; teach "kind = the extension," show a few examples. The LLM reads the extension off the value or omits the kind.

Both signals (`type.@this.Kinds` populated, vs a `Build` hook discoverable via `app.type.kind.@this`) already exist — the renderer just needs to branch.

### The vocabulary block (shape, not final copy)

```
## Types

Every value has a `name` and an optional `kind` (a sub-format). Pick `name` from this list.
Set `kind` when the step names a format, or take it from the value's file extension
(md, html, csv, jpg, mp4). Omit kind when there's none. Set `strict: true` only when the
step demands an exact format ("must be a gif") — verified only for binary formats.

  text       text of any kind; kind = format/extension (md, html, csv, json, xml, yaml)
  number     kinds: int | long | decimal | double
  bool
  datetime, date, time, duration, guid
  bytes, object, list, dict
  image      binary; kind = extension (jpg, png, gif, webp); strict-verifiable
  video      binary; kind = extension (mp4, webm, mkv)
  audio      binary; kind = extension (mp3, wav, flac)
```

`number` shows its kinds (not extensions); the media/text families say "kind = extension" because `Build` derives it.

## The `type` parameter

`variable.set.Type` becomes a `type` (`PLang/app/module/variable/set.cs`). In Action Detail it renders as `Type: type` (a reference into the vocabulary above), taught as the constructor:

```
type(name, kind?, strict?)   — emit name and kind as separate fields; NEVER the slash form "text/md"
```

The LLM emits `{"name":"text","kind":"md"}`. The three paragraphs of `as text` prose in `variable.set`'s Notes **collapse into the `type` entry's own description** — taught once, wherever a `type`-typed parameter appears.

## What to verify when implementing

- `TypeSchemas` (`app.builder.type.@this`) currently renders a scalar as just its input shape and a record as `{ k: T }`, reading `Shape`/`Fields`/`Values`/`Kinds` off the `type.@this` entity. Confirm whether `type` is best surfaced as a record (`{ name, kind, strict }` — the dict the LLM emits) or as a scalar with a `ConstructorSignature` of `name, kind?, strict?`. The constructor framing reads closer to `type(name, kind, strict)`; the record framing matches what the LLM emits. The existing renderer note warns the verbose scalar form misleads the LLM into emitting a dict — but for `type` a dict is exactly what we want, so the record path likely fits. Coder picks; the intent: the LLM emits `{"name":"text","kind":"md"}`.
- The system-prompt list must be **generated from the catalog**, not re-hand-written, so it can't drift from the per-step block again.
- Keep the `(kinds: …)` rendering for advertised types; add the "kind = extension" rendering for `Build`-hook types.
- Validate the result the way this plan was researched: force a fresh compile (`plang '--build={"files":[...],"cache":false}'`) of a goal referencing `text`/`number`/`image`, read the new trace under `.build/traces/<id>/`, and confirm the rendered vocabulary and the `type` entry read the way the LLM needs.
