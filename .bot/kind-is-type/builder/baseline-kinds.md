# Baseline: what Type each `as <type>` sentence saves today (before kind-is-type)

The builder, decision 746, on kind-is-type head `bd7ae851d` (binary from `51558b38c`; nothing in the decider/builder/templates changed since). Fresh scratch app with its own saved decider key, `"cache":"skip"`, **5 builds each**, the saved `Type` read from the `.pr` with jq (`variable.set`'s `Type` property; for the read sentence, the `variable.set` that captures `%!data%`). These are the "before" numbers the plan's kind tests are judged against.

## What the decider's Type question offered (the same for every sentence)

The question (per step) is:

> Step 0 is `<the step>`. which type does the step coerce the value to — none unless the step writes `as <type>`, `type=<type>` or `(<kind>)` (a value that merely looks like a dict/list/json is NOT a coercion; leave the type none)?

Its offered set (`criteria`) is the **top-level type registry** — the type *names* only:

```
app, app.type.root, item, text, number, list, dict, path, date, datetime, time,
duration, size, image, file, directory, url, guid, culture, input, separator,
permission, sensitive, setting, signature, status, table, tag, null, parallel, …
```

(the debug view truncates the value at ~1978 chars; the head is identical across all three sampled sentences — it is the type list, not the step's words). **It offers no kind** (`decimal`, `json`, `md`, `csv`, `int` are absent) **and no generic element** (`list<path>` is absent). So a kind or an element type is simply not reachable: the model can only pick the nearest top-level type, which is exactly what this branch is meant to change.

## The table

| sentence | saved `Type` (count /5) | one raw answer | what was wanted | why |
|---|---|---|---|---|
| `set %n% = "1.5" as decimal` | `number` **5/5** | `"number"` | `number`+kind `decimal` | `decimal` is a kind of number; not offered → base `number` |
| `set %files% = ["a.txt","b.txt"] as list of path` | `list` **2/5**, `path` **3/5** | `"path"` (and `"list"`) | `list<path>` | neither the generic nor the element survives; the model flips between the container and the element, never both |
| `set %x% = '{"a": 1}' as json` | `dict` **5/5** | `"dict"` | `text`+kind `json` | `json` is a kind of text; not offered → `dict` (issue 71) |
| `read 'notes.md' as text, write to %t%` | `{name:"file", kind:"md"}` **5/5** | `{"name":"file","kind":"md"}` | the read lands as `text` | `as text` is dropped; the read is typed by the file extension (`md`), so the value comes back parsed, not raw text (issue 20) |
| `set %d% = "PT30S" as iso duration` | `duration` **5/5** | `"duration"` | `duration`+an `iso` kind | the base `duration` is kept; the `iso` kind/format is not offered |

## Guards (should stay as they are)

| sentence | saved `Type` (count /5) | verdict |
|---|---|---|
| `set %n% = 5` (no cast) | `null` **5/5** | correct — no coercion when the step names none |
| `set %s% = "hi" as text` | `text` **5/5** | correct — a top-level type cast already works |

## Reading

- The three top-level casts that already name a type the registry offers — `as text`, and (implicitly) a bare number — behave right. The failures are all **kinds and generics**: `decimal` (kind of number), `json`/`md` (kinds of text), `list<path>` (a generic with an element). Each collapses to the nearest offered top-level type (`number`, `dict`, `file/md`, or `list`/`path`).
- `as list of path` is the only flaky one (2/5 vs 3/5) because two top-level types are plausible (the container `list`, the element `path`) and neither alone is what was asked; the rest are deterministic 5/5 because exactly one top-level type is the obvious fallback.
- The lever this branch names (kinds become types the builder can offer — `decimal` under `number`, `json` under `text`, `list<path>` as a real element-carrying type) would add those to the Type question's offered set; these counts are what to beat.

Harness: `/tmp/.../scratchpad/probes/` (not in the tree); the decider key re-saved into that scratch app's own `/.data/setting/data.sqlite`.
