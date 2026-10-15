# Stage 2 shape — lists & dicts as a readable table, the row cap, and the OpenAi wiring

Coder: tester (plang-f2). Decisions 800, 802. Builds on the door (1b).

## What stage 2 delivers

1. A list of records written into an llm message renders as a **markdown table** the agent can read:
   its columns with their types, then up to `%!llm.setting.rows%` rows, then `--- N more rows ---`.
2. A dict renders as its keys with their types (then its values), cut the same way.
3. A scalar list (`[1,2,3]`) renders as a one-column table / inline list, cut the same.
4. The door is wired into `llm.query` so an actual `ask llm "rank these %orders%"` cuts per value,
   end to end through the request body (OPEN 1 from 1b).
5. Text cells still obey `%!llm.setting.length%` (a long cell is char-cut with its marker).

## The writer assembles from the primitive stream (OBP: it never inspects the value's class)

A list/dict writes its own shape through the writer's primitives it already calls —
`BeginArray` / `BeginObject` / `Name` / the scalar primitives (`Int`, `String`, `DateTime`, …) /
`EndObject` / `EndArray`. The **llm.Writer** reacts to that stream; it does not type-switch on the value:

- `BeginArray` → table mode. The writer buffers elements (one object = one row) up to `rows`+1 to
  learn the columns and detect overflow, then emits the table:
  - **records** (each element opens `BeginObject`): columns = the first record's `Name`s; each
    column's **type** is read from the scalar primitive its cells call (`Int`/`Long`/`Double`/
    `Decimal` → `number`, `String` → `text`, `DateTime` → `datetime`, `Bool` → `bool`, `Bytes` →
    `binary`, nested `BeginObject`/`BeginArray` → `dict`/`list`). Header, type row, up to `rows`
    data rows, then `--- N more rows ---` when more came.
  - **scalars** (first element is a scalar): a single column (or inline), cut the same.
- `BeginObject` at top level (a dict) → its keys with their types, then its values (each value
  char-cut by `length`); a dict with more than `rows` keys cut the same with `--- N more keys ---`.
- scalars/text as stage 1 (char cut at `length`).

Type inference from the primitive is the writer deciding *how it looks* — the value still just writes
what it is. No new IWriter members (the existing primitives carry it) → **no locked-surface change**.

### Proposed markdown (to pin with you/Ingi)

```
| id (number) | total (number) | date (datetime) |
| --- | --- | --- |
| 1 | 9.99 | 2026-01-15 |
| … 100 rows … |

--- 50 more rows ---
```

(A dict:)
```
- id: number
- name: text
- active: bool
```
then its values, cut the same.

## The row cap setting

`%!llm.setting.rows%` — `[Out, Store] number`, default **100** (the plan's number; one word beside
`length`, per your note). Moves the cut.

## Wiring the door into OpenAi (OPEN 1)

`llm.query` stops capping the pre-resolved content string (stage 1's stopgap). Instead each message's
content is rendered through the **llm.Writer** as a VALUE: the content text template is `Output`
through the llm writer, so an embedded `%orders%` arrives as a list and the writer tables+cuts it
(full-match `%orders%` and partial `"rank these %orders%"` both, via the door). This needs the
content as the template it is, before `Message.Value()` flattens it through text's own writer — the
content-as-value step. I'll pin the exact access (enumerate the message list's rows, take each
content template, `Output` it, rebuild the `LlmMessage` with Role + rendered Content + Images) when
building, red-first.

## Tests (red for their own reason; end-to-end first, per the architect)

- **RankOrders_EndToEnd** (first): `ask llm "rank these %orders%"`, `%orders%` = 150 records of
  {id, total, date} → the OUTGOING request body shows `id (number), total (number), date (datetime)`,
  100 rows, `--- 50 more rows ---`. RED without stage 2 (today: json blob, whole-string capped).
- **DictGivesKeysAndTypes**: `describe %cfg%` → keys with types, values cut.
- **ListUnder100_AllRows_NoMarker** (guard); **RowsSettingMovesTheCut**.
- Writer-level units: a `list<record>` through llm.Writer → the table + `--- N more rows ---`;
  the partial `"rank these %orders%"` via the door (per-value).

## Checks tests can't show (I'll run + report)

- The builder's own llm.query content is text, not record-lists — the table path doesn't change its
  prompts (decider/builder prompts stay byte-equal).
- Recorded llm answers don't move where values are under the limits.
- Gate by name; PLNG001/002 = 0; plang suite fail list still identical to runtime2.

## OBP validation

| new surface | check |
|---|---|
| table/keys rendering | the list/dict writes its shape through the writer's existing primitives; llm.Writer reads the stream, never the value's class; column types from the primitive called |
| `rows` limit | one llm setting, dot path `%!llm.setting.rows%` |
| OpenAi wiring | the content rides as a value through the door; no value asks which writer |

## Open sub-decisions

- a) the exact markdown (table above) — your/Ingi's to pin, esp. whether types are a header suffix
     `id (number)` or a separate type row.
- b) a dict's cut: `--- N more keys ---` symmetric with rows — ok?
- c) a nested cell (a record whose field is a list) — render as `[list:3]` / `{dict:4}` summary,
     never inlined recursively? (keeps the table flat and bounded)
