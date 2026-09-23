# Births inventory — where items are created (read-only, at `22e6dafd6`)

**Method:** a Roslyn pass over the full PLang compilation. That means MSBuild's own compile items and references, plus all 15 source generators run through a driver, giving **0 compile errors**, so every site is semantically bound. Grep would miss implicit conversions and target-typed `new(...)`. Every operation tree was walked. An *item* is any type deriving from `app.type.item.@this`, and that includes the error types, `app.type` and the program nodes.

Raw rows: `births-inventory.tsv`, one row per site with columns `group  file:line  what  enclosing-member`. Generated files are prefixed `GEN:`. The analyzer lives in the job tmp dir (not committed).

Counts are **production source only**. Generated code is noted separately, since its sites are a handful of emitter templates in `PLang.Generators`, not hand-written code. Tests are excluded (size at the end).

## Totals

| # | Door | Sites (src) | Notes |
|---|------|------------:|-------|
| 1 | Readers (`ITypeReader`) + json parser | **68** | json.cs 7, action Reader 5, bool 4, type 3, number 3, goal 3, …, spread over 25 readers |
| 2 | Typed ask `Create(raw, data)` / `(…, ctx)` | **39** | base `item.Create(raw,data)` 17 · `app.type.Create(name,…)` 9 · per-type static 6 (tag, list, image, guid, duration, dict) · `test.Create` 1 · generic `ICreate<T>.Create` 6 (data.Value ×2 `data/this.cs:520-521`, type.Create ×2 `type/this.cs:375,379`, ICreate defaults ×2) |
| 3 | Pure core `T.Create(object raw)` | **4 external** + 29 self | external: `module/action/test/tag.cs:25`, `test/list/this.cs:64`, `test/this.cs:42` (all tag, all from today's rework), `type/item/number/serializer/Reader.cs:25`. Self: each value type's `Create(raw,data)` delegating to its own pure core (number 4, time/text/tag/guid/duration/datetime/date/binary/bool 2 each, …) |
| 4 | Implicit conversions INTO an item | **237** | see operator table |
| 5 | Direct `new X(…)` outside X's own type | **526** | +133 inside the type's own members (not counted); generated +547 (all errors) |
| 6 | Result doors carrying a raw CLR value | **150** | `context.Ok(raw)` 60 + `Ok(object)` 8 + `new Data(name, raw…)` 82; generated +588 `Data(raw)` +238 `Ok(object)` |

### 4 — operators IN (CLR → item) and their site counts

| Operator | Sites |
|---|---:|
| `number` ← 15 CLR numerics (`type/item/number/this.cs:83-97`) | 187 (int 60, double 19, decimal 16, long 8, BigInteger 6, sbyte/byte/short/ushort/uint/ulong/Int128/UInt128 4 each, float 3, Half 3) |
| `@bool` ← bool (`bool/this.cs:32`) | 41 |
| `text` ← string (`text/this.cs:222`) | 31 |
| `choice<T>` ← T (`choice/this.cs:48`) | 10 (Visibility 3, HttpMethod 2, Format, StreamFormat, Level, ErrorOrder, Mode 1 each) |
| `duration` ← TimeSpan (`duration/this.cs:64`) | 5 |
| `binary` ← byte[] (`binary/this.cs:61`) | 3 |
| `guid` ← Guid (`guid/this.cs:61`) | 0 |

15 of the number sites are inside `number/this.cs` itself. Not items and not counted: `Data<T>` ← T (`data/this.cs:881`), `Operator` ← string, `parameter.list` ← `List<Data>`.

**Operators OUT (item → CLR), out of scope:** 54 sites.
- `variable` → string: 29
- `choice<T>` → T: 21
- `number` → int: 3
- `path` → string: 1
- `duration` → TimeSpan: 1

### 5 — direct `new`, by category (526 src)

| Category | Sites | Biggest |
|---|---:|---|
| errors (`app.error.*`) | 270 | ServiceError 105, Error 74, ActionError 53, ValidationError 16, AssertionError 11, PermissionDenied 4 |
| values | 120 | text 23, list 20, dict 10, @null 10, binary 7, list<test> 6, StatInfo 5, file 5, datetime 5, @bool 4, list<text> 3, list<goal> 3, … |
| domain items (`app.*`) | 61 | module.action.list.type.list 10, event.moment 7, type.clr 6, test 6, llm.LlmMessage 6, … |
| type entity `new app.type(…)` | 49 | see below |
| program nodes (`app.goal.*`) | 26 | action.list 8, step.list 7, … |

Generated: 547 more, all errors (Error 240, ServiceError 181, ActionError 126) from the handler plumbing templates.

### 6 — result doors (already context-bound)

- `context.Ok(raw)` src by raw type: bool 31, string 17, int 3, byte[] 2, app 2, and 1 each of long, TimeSpan, Assembly, channel.goal, string?.
- `new Data(name, raw…)` src: string 25, object? 21, int 10, bool 7, `Dictionary<string,object?>` 5, string? 4, T 3, HttpMethod 2, …
- Generated: object 178, string 150, bool 84, int 72, bool? 31, number.Precision 21 — the `[Default]`/parameter plumbing.

Overlap with 4/5: most of the 237 implicit-in sites and the 120 value `new`s are *arguments* to a result door or a `Data` ctor, which already has a context. See the busiest files: `assert/code/Default.cs` is 10 4-in + 10 5-new + 10 Ok. Those could become the result door taking the raw value.

## `Type` built by the item itself, and `new app.type(…)` outside the registry

- **34 `override Type => new(...)` getters.** Every value type (archive, base64, binary, bool, choice, computed, date, datetime, dict, duration, file, guid, image, list, null, number, path, signature, tag, text, time, url) plus goal, step, action, modifier, snapshot, type.clr, type.table, crypto hash, list-action `list`, output.ask, code.registration, code.defaultoverride. There are 2 more `Type` getters building one: base `type/item/this.cs:279` and `type/kind/this.cs:53`.
- **Other `new app.type(…)` outside the registry:**
  - `data/this.cs:191` (`Is`)
  - `format/list/this.cs:447,457,464` (`TypeFromMime`) and `:478` (`TypeFromExtension`)
  - `channel/type/message/this.cs:21` (`Ask`)
  - `this.SnapshotWire.cs:36` (`SnapshotFromWire`)
- **Registry itself:** `type/list/this.cs:185,205,206` (indexer) and `:473,528,542` (`BuildTypeEntries`, the verb+noun from #14).

## Ten busiest files (src, groups 2–6)

| Sites | File | Mix |
|---:|---|---|
| 32 | module/action/llm/code/OpenAi.cs | 11 new, 8 implicit, 8 Data(raw), 3 Ok, 1 Ok(obj), 1 ask |
| 30 | module/action/assert/code/Default.cs | 10 new, 10 implicit, 10 Ok |
| 29 | module/action/signing/code/Ed25519.cs | 20 new, 4 implicit, 3 Data(raw), 2 Ok |
| 26 | type/item/path/file/this.Operations.cs | 21 new, 2 implicit, 2 Ok, 1 Data(raw) |
| 23 | module/action/build/code/Default.cs | 10 new, 6 implicit, 4 Ok, 3 Data(raw) |
| 22 | module/action/http/code/Default.cs | 12 new, 4 implicit, 5 Ok, 1 Data(raw) |
| 19 | type/item/number/this.cs | 15 implicit (own), 4 new |
| 18 | goal/this.cs | 13 new, 5 implicit |
| 17 | module/action/test/discover.cs | 10 new, 6 implicit, 1 ask |
| 16 | type/item/path/http/this.cs | 10 new, 2 implicit, 3 Ok, 1 Data(raw) |

## Tests (not counted; rough grep)

516 test files. 232 `new global::app.type.item.X(` and 202 `new Error/ServiceError/ActionError(`. Test sites also use target-typed `new(...)` and implicit conversions, so these are lower bounds.

## Caveats

- Group 6's `Data(name, raw)` rule counts a `value:` argument whose static type is neither an item nor a Data. `object?` sites may carry an item at run time, so treat those 21 (src) as an upper bound.
- "Own type" (the 133 excluded from group 5) means the creation sits inside the created type's own members, including nested types. A type's private factory helpers land there.
