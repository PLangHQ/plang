# Topic — restructuring the type information sent to the LLM

This is the part Ingi asked to get right. Grounded in the actual compile prompt captured from a fresh build (2026-05-30), not a sketch.

## Today — three surfaces that disagree

Captured from a real `--build` of `Tests/Simple`:

1. **System prompt** (`os/system/builder/llm/Compile.llm`, stable for caching) — a hand-written valid-type list at line 235: `string, int, long, float, double, decimal, bool, datetime, timespan, guid, json, list, dict, bytes, object, ...`.
2. **Per-step user message** (`CompileUser.llm`) — a flat line repeated in *every* step's message: `Primitive types: string, int, long, float, double, decimal, bool, datetime, date, time, duration, guid, byte, bytes, list, array, dictionary, object, json`. Says `string`, not `text`. Lists `int/long/float/double/decimal` as separate primitives. **`number` is not in it.**
3. **Per-step catalog block** (`CompileUser.llm`, scoped to the step's actions) — `Catalog types referenced by this step's actions:` rendered by `TypeSchemas` (`app/builder/Types/this.cs`). For the `set` step it rendered `object: string`. This is where `(kinds: …)` shows; `number` would render `number: string (kinds: int | long | decimal | double)` when a step references it.

The three disagree: `int` appears as a primitive in (1) and (2) *and* as a kind of `number` in (3). `string` in (1)/(2) vs the `text` we're moving to. The `as text` mechanism isn't represented as a type at all — it's three paragraphs of prose in `variable.set`'s Notes mapping "as text" → `Type="string"`.

## Target — one surface, two render modes

Collapse to a single type vocabulary, and split it by *what varies per build* rather than by primitive-vs-domain:

- **Cached system prompt — the universal vocabulary.** The core type names + their kinds are goal-invariant, so they belong in the stable system prompt (cached by the provider), generated from the catalog, replacing the hand-written list at `Compile.llm:235`. This is the "what kinds exist + how to set a kind" teaching, written once.
- **Per-step user message — only step-specific types.** Keep the scoped block, but it now carries *only* the domain/record/enum types this step's actions reference (`path`, `llmmessage`, enums, records). The flat `Primitive types:` line is **removed** — its content moved into the cached vocabulary.

This fixes the redundancy (the flat list repeated per step), improves caching (vocabulary stops varying), and gives the LLM the kind knowledge it currently lacks.

### Two render modes for kinds

The renderer (`TypeSchemas`) gains a second mode, keyed off signals already on the types:

- **Advertised** — the type declares a static `Kinds` list (only `number` does today). Render `number — kinds: int | long | decimal | double`. The list is a closed vocabulary the LLM picks from. These aren't extensions.
- **Extension-derived** — the type has a `Build(value)` hook but no `Kinds` list (`image`, and `text` once built). Render `image — kind = file extension (jpg, png, gif, …)`. Don't enumerate exhaustively; teach "kind = the extension," show a few examples. The LLM reads the extension off the value or omits the kind.

Both signals (`Kinds` static, `Build` hook presence) already exist on the types — the renderer just needs to branch.

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

`variable.set.Type` becomes a `type`. In Action Detail it renders as `Type: type` (a reference into the vocabulary above), taught as the constructor:

```
type(name, kind?, strict?)   — emit name and kind as separate fields; NEVER the slash form "text/md"
```

The LLM emits `{"name":"text","kind":"md"}`. The three paragraphs of `as text` prose in `variable.set`'s Notes **collapse into the `type` entry's own description** — taught once, wherever a `type`-typed parameter appears, instead of duplicated per action.

## What to verify when implementing

- `TypeSchemas` currently renders a Scalar as just its input shape and a Record as `{ k: T }`. Confirm whether `type` is best surfaced as a Scalar with a `ConstructorSignature` of `name, kind?, strict?` (the constructor framing) or a Record `{ name, kind, strict }`. The constructor framing reads closer to Ingi's `type(name, kind, strict)`; the Record framing is what the LLM emits (a dict). The note in `TypeSchemas` warns that the verbose Scalar form misleads the LLM into emitting a dict — but for `type` a dict is exactly what we want, so the Record path likely fits. Coder picks; the intent is "LLM emits `{name, kind, strict}`."
- The system-prompt list must be **generated from the catalog**, not re-hand-written, so it can't drift from the per-step block again.
- Keep the `(kinds: …)` rendering for advertised types; add the "kind = extension" rendering for `Build`-hook types.
