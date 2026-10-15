# llm-writer

Traces to: the conversation with Ingi on 2026-10-15, logged in `.bot/kind-is-type/architect/summary.md` as decision 791 (and 793, which starts this branch). Chain: idea → conversation → summary → this plan → code (`start.goal` and the tests it runs).

## Why

A value put into an LLM message is written whole. `user: %text%` with a 2 MB text, or `%orders%` with 40,000 rows, can blow the model's context, cost money for nothing, or fail the call. Ingi: "I feel like there should be a llm serializer … text can be anything, so when you do 'user: %text%' to the llm, it can blow up, so the llm module uses llm serializer, and for text it limits it to a length, for dict/lists, it trims".

## What the developer gets

- A value goes into an LLM message whole, unless it's over the max (Ingi: "%doc% just goes through unless we go over max").
- A list over 100 items is cut, with a marker the LLM can read: `--- 50 more rows ---`.
- A list or a dict tells the LLM its column names and their types first (Ingi: "very usefull for llm").
- A text over the max is cut with the same kind of marker.
- Binary, images and files are never inlined; the LLM sees what they are (`[image/png, 2.1 MB]`).
- `%x.trim%` gives a value trimmed, as the LLM would see it (Ingi: "same intent, we want it trimmed. what is trimmed is non of our business").
- The limits are llm settings, so a developer raises them where needed.

## The rulings (Ingi, 2026-10-15)

1. **A value writes what it is; the writer decides how it looks** (the standing rule). The LLM writer is a writer beside json, formal and text (`type/item/kind/json/writer.cs`, `goal/step/action/formal/writer.cs`, `type/item/text/writer.cs`). No value changes and no value asks which writer it's written to.
2. **The writer is the llm module's** (a module owns its own types). Nothing is added to the core's serializer.
3. **Whole up to the max.** There's no per-query "whole" switch: a developer raises the setting.
4. **A list over 100 is cut with `--- N more rows ---`**; a list or a dict gives its column names and types first.
5. **`trim` is one word with one intent: the value trimmed.** What trimming means is the value's own business: a list keeps its first rows and says how many more, a text its whitespace as today, and for the LLM the writer cuts what's over the max.
6. **A real summary** (`%doc.summary%`, written by an LLM) is a follow-up, not this branch (Ingi: "maybe followup").

## Stages

Each stage ends with its tests built and run; each test goes red without its change. Each stage's shape comes to the architect before it's built.

### 0. Trace: where a value becomes LLM message text today

Which door renders `%var%` inside an llm message (the message template, `llm/type/message/this.cs`, `llm/query.cs`) and through which writer; what a dict, a list, a table and a file write as there today. The shape of stages 1–3 follows from it.

### 1. The LLM writer and its limits

```
- set %!llm.setting.length% = …              → the max for a text, as characters (a default the trace proposes)
- ask llm "say hi to %name%"                  → %name% whole, as today
- ask llm "summarize %doc%"  (%doc% over the max)   → the first part, then "--- 12,400 more characters ---"
```

### 1b. A value inside a sentence (decision 800)

A partial template ("rank these %orders%") renders through a text writer text makes for itself (`type/item/text/this.cs:163–167`), so the llm writer never sees `%orders%`. Ingi approved one `IWriter` door for a text with values inside it: text hands the writer its literal parts and its values in order; the text and json writers write them as one string, as today; the llm writer writes each value through its cuts. A locked serializer addition, re-pinned under 800.

```
- ask llm "rank these %orders%"               → the same cuts as %orders% alone
- write %greeting% to a json channel          → one string, byte-equal to today (a guard)
```

The default for `%!llm.setting.length%` is 100,000 characters (Ingi), above the builder's longest prompt with room.

### 2. Lists and dicts

```
- ask llm "rank these %orders%" (%orders% has 150 rows of id, total, date)
    → columns: id (number), total (number), date (datetime); then 100 rows; then "--- 50 more rows ---"
- ask llm "describe %cfg%" (a dict)           → its keys with their types, then its values, cut the same way
```

### 3. What isn't text

```
- ask llm "what is %photo%"  (an image)       → "[image/png, 2.1 MB]", never its bytes
- ask llm "read %report%"    (an unread file) → its name, type and size
```

### 4. `%x.trim%`

```
- write out %orders.trim%                     → the same view the LLM gets: columns, 100 rows, "--- 50 more rows ---"
- set %t% = %text.trim%                       → a text's own trim, as today
```

## Demolition

Nothing is removed in this branch, unless the trace finds an ad hoc cut that the writer replaces (each one listed by the trace, with its file:line).

**Stays:** every value's own writing; the json, formal and text writers; debug's own line cut (`%!debug.setting.length.max%`, `module/debug/setting/length/this.cs:7`), which may move onto this writer later.

## What tests can't show (checks the coder runs and reports)

- The decider's and the builder's prompts are byte-equal (the decider pins), since the builder's own prompts aren't llm message values.
- The llm tests' recorded answers don't move, where the values they send are under the limits.
- Every stage: the gate by name; PLNG001/002 at 0; the plang suite no worse than runtime2's.

## OBP validation

| new surface | check | result |
|---|---|---|
| the LLM writer | a writer of the llm module; no value asks which writer it's written to | to verify at stage 1 |
| the limits | llm settings, one per limit, named by dot path | to verify at stage 1 |
| columns and types | the list or dict writes its own shape through the writer's primitives; the writer doesn't inspect the value's class | to verify at stage 2 |
| `trim` | one member, each value answering what trimmed means for it | to verify at stage 4 |

The coder owns the code shape; the sketches here are intent.
