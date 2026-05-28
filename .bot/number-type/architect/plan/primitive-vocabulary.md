# Primitive vocabulary — open thinking

**Status:** discussion, not decided. Captured from a 2026-05-28 conversation with Ingi while we were on the `data-normalize` branch. Lands here because `number` is one entry in this larger question and the answer affects how that entry should be scoped.

## The seed

Ingi: *"i think we need to define primitive data types in plang, there are the default that c# has, but i would like to add timespan, datetimeoffset (no datetime), number (int, decimal etc, see number-type branch), maybe some more, i think it would be good to have this well defined and it is not today"*

Then, after we sketched it: *"yes on extending, i would say date and time instead [DateOnly/TimeOnly], what about image, video, code, ... bit high level, we had some class that mapped all kinds of high level name to more granular, maybe we deleted it"*

## What's actually there today

The class Ingi remembered isn't deleted. It's `app/formats/this.cs` — alive, 30+ Kinds (image, video, audio, code, document, archive, spreadsheet, presentation, ebook, font, certificate, calendar, gis-data, executable, machine-learning, …). Every extension maps to a Kind and a MIME.

But `app.formats` lives **next to** `app.types.Primitives` (`PLang/app/types/this.cs:34-77`), not inside it. They never got married. Today `Formats.KindOf("image")` returns `"image"`, but `Types.Get("image")` returns null. The LLM cannot pick `image` as a type even though every other part of the system knows what an image is.

Other half-finished work in the same area:

- `IsPrimitive` at `PLang/app/types/this.cs:430-431` accepts `DateTimeOffset`, but the name table has no entry for it. So a domain type can carry DateTimeOffset and round-trip, but the LLM can't pick it by name. Asymmetric.
- The name table has `datetime`→`DateTime` and `date`→`DateTime`. Ingi wants DateTime banished entirely.
- `time` and `timespan` are aliases for the same CLR type (`TimeSpan`). `time` is a footgun because it could equally mean time-of-day — the LLM can't tell from the name.
- TimeSpan parsing lives in `Conversion.cs:307-318`. The JSON converter lives in `app.channels.serializers.TimeSpanIso8601`. The boolean rule lives via `IBooleanResolvable` if anywhere. Three sites for one primitive.

## The architectural realization

We've been calling this "primitives" but there are three concepts collided under that word:

1. **Wire-level CLR types** — DateTimeOffset, decimal, byte[]. What the runtime physically holds.
2. **Named picks** — what the LLM picks: `number` (collapses int/long/decimal/double), `datetime`, `image`.
3. **Format kinds** — the semantic category that owns transformation rules (compression, display, action surface).

Today (1) is the *values* of the `Primitives` table, (2) is the *keys* (incomplete), (3) lives entirely in `app.formats`. Three locations, no single owner. The reason `image` isn't a type today isn't a decision against it — there's no slot in the schema for a thing that's both a named pick and a format kind.

Adding a primitive today touches six files: `Primitives` table, `IsPrimitive`, `Conversion.TryConvertTo`, sometimes a JSON converter, sometimes `IBooleanResolvable`, comparison/arithmetic in the operator pipeline, display in channels. No declaration site means every addition risks the same half-done state DateTimeOffset is in.

## The model that fell out of the conversation

Once you let category kinds in, the vocabulary isn't flat anymore. Something like:

- **Scalars** — number, string, bool, datetime, date, time, duration, guid
- **Bytes-family** — bytes, image, video, audio, archive, font, executable, …
- **Text-family** — text, code, html, json, xml, csv, markdown, …
- **Collections** — list<T>, dict<K,V>
- **Object** — opaque

A bytes-family member is `byte[]` plus a MIME-family constraint. A text-family member is `string` plus a content-shape constraint. They don't replace `bytes` and `string` — they refine them. The LLM picks `image` when it knows the bytes are an image; it picks `bytes` when it doesn't care.

This is the right model for an LLM-driven language because the LLM thinks in categories, not in C# types. `image.resize` declaring its input as `image` is a typed contract; today it would have to say `bytes` and validate at runtime. Lifting categories into the type system makes action signatures honest.

## Specific resolutions from the conversation

Confirmed:

- **DateTime is banished.** Keep the LLM-facing name `datetime` (familiar token); rebind to DateTimeOffset.
- **`date` becomes DateOnly** (calendar date).
- **`time` becomes TimeOnly** (time-of-day).
- **`duration` becomes TimeSpan.** Rename away from the `time`/`timespan` overload.

Open:

- Whether to lift category kinds (image, video, code, …) into the type vocabulary in this branch, or treat that as a separate arc.

## The fork

Two arcs are visible:

**Scope X — settle the foundation.**
Clean up the base layer cleanly: number (already planned in this branch), `datetime` → DateTimeOffset, `date` → DateOnly, `time` → TimeOnly, `duration` → TimeSpan. Build the OBP shape — `app/types/primitive/<name>/this.cs` — so each base primitive owns its parser, serializer, truthiness, comparison. No category lifting yet. This is the lever: once the shape exists, category types are additive.

**Scope Y — lift the categories.**
After X. Bring `formats` kinds into the primitive registry as a second tier (bytes-family, text-family). Carve out action surfaces folder-by-folder (`image.*`, `code.*`, `document.*`). This is the long arc — multi-month, scope grows as we learn which categories the LLM actually wants.

The scope-balloon warning matters: lifting `image` into the vocabulary only pays off if there's an action surface that consumes `image` (not `bytes`). Adding the type without the actions is just a label.

## The sharp question

**Does this look like one arc (X then Y, planned together, carved as ~4 stages) or two arcs (X this branch, Y as separate work in a few months once we know more)?**

My instinct says two — X is concrete and bounded; Y is open-ended scope that benefits from learning what categories the LLM actually wants in practice. But there's a case for planning them together so the second tier is designed in, not bolted on.

## How this lands on this branch

If we go **two arcs**:
- This branch (`number-type`) stays as scoped: `number` only. The OBP shape for `app/types/number/` becomes the first instance of the per-primitive pattern. Don't generalize it preemptively. After it ships, a follow-up branch carves the shape into `app/types/primitive/<name>/` and migrates the rest.

If we go **one arc**:
- This branch grows. `number` is still Stage 1–4 as drafted; before that we carve a Stage 0 that introduces the `app/types/primitive/<name>/this.cs` shape and migrates existing scalars (string, bool, datetime → DateTimeOffset, date → DateOnly, time → TimeOnly, duration, guid, bytes) into it. Then `number` slots in as one more folder. Category lifting still becomes a follow-up; the foundation is shared.

The cost difference is roughly: two arcs = `number` ships sooner, less infrastructure churn this branch, foundation built later when we know more. One arc = consistent shape from the start, but `number` ships behind a wider migration.

## Notes carried forward

- The `formats` table has the kinds already. If we lift them, `app.formats` becomes the *resolution helper* (extension → primitive name), not a separate universe. The MIME family lives on the primitive itself.
- `image` vs `image/png` — the catalog should probably accept both: `image` as a category match (any subformat acceptable) and `image/png` as a refinement. Mirrors MIME's type/subtype shape, mirrors how the LLM thinks ("an image" vs "a PNG").
- `code` parallels `image`: bare `code` is the category, `code/csharp` or `code/python` are refinements. Action signatures can declare which.
- `text` as a type-family is *not* the same as `string`. `string` is "raw character sequence"; `text` is "human-readable content with a content-shape" (markdown, html, json, etc.). A category-aware `text` lets the LLM pick the right renderer/parser.
- None of this is a current-branch deliverable. It's framing for the conversation when Ingi gets back to it.
