# Stage 1 — reachable-set trace (the gate, logged before any reader is written)

**Branch:** `value-construction-redesign` · coder · 2026-06-29
**Question:** which `(type, kind)` does construction route through a `source` (case 3, raw → reader)? Those — and only those — need an `ITypeReader`.

## Method

Two construction surfaces deliver a value + declared type:
- **case 3 (raw → `source` → reader):** a raw `string`/`byte[]` + declared type. After the redesign these mint a `source` and parse through the reader registry (`Readers.Reader(name, kind)` → throws `NotSupportedException` if no `ITypeReader`).
- **case 2b (built → `Convert` hook):** an already-built `item.@this` of a *different* type. Uses the type's `Convert` hook, **not** a reader.

So a type needs a reader **iff** a raw form is ever constructed against it.

## The decisive trace — how `set %x% = "2026-01-01" as date` is stored and run

`.pr` (`Tests/ScalarsAsNative/Stage3/.build/datereturnsdateonly.test.pr`):
```
Value param: type={name:text},  value="2026-01-01"     ← stored as TEXT
Type  param: type={name:type},  value={name:date}       ← the declared target
```
- **Today:** `Value.Value()` → `text "2026-01-01"` (text reader, exists). `set.cs:264` calls `date.Convert("2026-01-01")` — the **eager Convert HOOK** (not a reader). date never touches a reader today; only `text` does.
- **After Stage 4** drops `set.cs:264`'s eager convert: the ctor gets the raw string + `date` type → **case 3** → mints `source("2026-01-01", date)` → `Readers.Reader("date")` → **throws today (no date reader).**

⇒ date/datetime/time readers are **required by this branch** — without them every `as date/datetime/time` breaks. Confirmed against the live `.pr` + `set.cs` + the empty reader registry (no date/datetime/time registration anywhere).

## The `as T` vocabulary (builder-authored)

`primitive.@this.BuilderNames` (`type/primitive/this.cs:157`) = the LLM's `as T` set:
- inline: text, number, bool, list, dict, **datetime, date, time**, duration, guid
- reference: image, video, audio, path, bytes
`set`'s `as T` resolves via the catalog (`App.Type.Get`), not bounded to BuilderNames, but authored goals use BuilderNames; reference fundamentals are written as a path/handle, never an inline literal.

## Reachable-set result

| Type | Has `ITypeReader`? | Reached by case 3 (raw)? | Action |
|---|---|---|---|
| text, number, bool, list, dict, duration, guid, path, binary, image, code, object, item, table | yes | yes (literals) | none — covered |
| **date** | **no** | **yes** (`as date` literal; writes as String) | **add reader** (1.1) |
| **datetime** | **no** | **yes** (`as datetime`; writes as DateTimeOffset) | **add reader** (1.1) |
| **time** | **no** | **yes** (`as time`; writes as String) | **add reader** (1.1) |
| bytes | →`binary` (reader ✓) | yes | none |
| csv/txt/xml/yaml/yml | →`text` (reader ✓) | yes | none |
| **video, audio** | no folder/alias | **unconfirmed** | 1.2 — verify how `as video`/`as audio` resolves (likely binary/image kind); no reader if it maps to a reader-bearing family |
| **choice / enum** | no | **only via case 2b** (schema → Declare/validateResponse → built value → `choice.Convert` hook, keyed by enum name) | 1.3 — confirm `as <Enum>` is never an authored case-3 literal; if it is, register an enum-name-keyed reader |
| **file, directory, url, permission** | only `Default.cs` | **no** — constructed from a **pre-built** wrapper (`new file.@this(path)`, `new image.@this(bytes,path)`), case 2 | **no reader needed for construction** — record exclusion |
| archive, object*, code*, item* | (object/code/item have readers) | n/a | none |

\* object/item/code have no `Convert` hook but ship readers; fine.

## Conclusions

- **Required readers (case 3, raw, no reader today): `date`, `datetime`, `time`.** This is the core Stage-1 work (Step 1.1).
- **file / directory / url / permission: NOT reader gaps.** They are handed pre-built native wrappers at construction (case 2), confirmed by the Explore trace of `file/read.cs:76,100` and the absence of any raw `string`→reference-type construction. The plan's worry about a `NotSupportedException` leak does not bite — they never mint a `source`. (If a future authored `as file` literal appears, revisit.)
- **choice / enum: confirmed NOT a reader gap (1.3 resolved).** Scanned all `Tests/**` + `system/**` `.goal` files for `as <Enum>` — none exist (every authored `as T` is a fundamental). Enums reach construction only via schema → Declare/validateResponse → **built** value → case 2b → the `choice.Convert` hook (keyed by the enum's name). No reader needed.
- **video / audio: confirmed NOT a reader gap (1.2 resolved).** No `as video` / `as audio` authored anywhere; they resolve by **mimetype** to the binary family (`catalog/this.cs:341-342`), which has a reader. No folder/reader to add.

## Defense-in-depth (1.4)

`Readers.Reader` throws `NotSupportedException`, which `source.Value`'s catch (`source.cs:98`) does not cover. Totality (above) is the primary fix. Decision: **do not** broaden `source.Value`'s catch on this branch — read-path-unification is relocating `MaterializeFailed` authoring into `app.type.Create(source)`; a missing reader should stay a loud `NotSupportedException` (a genuine coverage gap), not be silently downgraded. Re-confirm at merge.

**Gate status: PASS.** Readers needed = {date, datetime, time}. choice/video/audio = investigate (1.2/1.3), default no-reader. file/directory/url/permission = excluded with trace.
