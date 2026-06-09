# Test coverage — typed value model

Three sections: the coverage matrix (one test per row), the failure matrix (negative paths), the new-surfaces inventory. Coder reads this while implementing per-stage tests; test-designer reads it to write the suite. Layer: **C#** (TUnit) / **goal** (`.goal` under `Tests/`) / **int** (a cut from `test-strategy.md`). Sense: **green** / **neg**.

## 1. Coverage matrix

### Stage 1 — `Comparison` enum
| Behavior | Layer | Sense |
|---|---|---|
| enum is exactly `{Less,Equal,Greater,NotEqual,Incomparable}`, no sign relied on | C# | green |

### Stage 2 — value door + `.`/`!` resolver
| Behavior | Layer | Sense |
|---|---|---|
| `await data.Value()` returns the **typed** value for an authored scalar (`set %x%=5` → a `number`, not raw `5`) | C# | green |
| `Value()` completes synchronously when present (`IsCompleted`, no async hop); async load only when pending | C# | green |
| no public sync `.Value`; a value-type's own backing read is private | C# | green/neg |
| `Peek()` returns the current rung unparsed (bytes/text), no materialise | C# | green |
| `_raw` dissolved — a bare value off a channel refines `bytes → item` in place | C# | green |
| `.` resolves the data plane, `!` the property plane; the **type** answers both | C# | green |
| `%x.size%` (data key) and `%x!size%` (property) are distinct, no shadowing | goal | green |
| no generic `ToRaw`; `text` raw string is private | C# | neg |
| a `%var%` ref and a raw JSON container both ride as typed PLang values (`text`/`dict`/`list`), never a bare C# `string`/`Dictionary` | C# | green |
| `Action.GetParameter<T>(name)` returns a **lazy** typed `Data<T>`; getter access doesn't read — `await Param.Value()` triggers resolution + the content read | C# / int(2) | green |
| the param resolution-error guard fires **after** `await Param.Value()` — a bad-scheme / unset-`%var%` param yields a typed error from the resolved Data, not an NRE on `.Value!` | C# | neg |
| `ToString`/`Equals`/`GetHashCode` read the already-materialised backing only — never navigate or trigger a read | C# | green/neg |
| navigation (`GetChild`/`Variable.Get`/`Variable.Resolve`) is `ValueTask`, sync-completing in memory, awaits only the first content read; awaited once (no store-and-await-twice / `.Result`) | C# | green |

### Stage 3 — reference types (`file`/`directory`/`url`)
| Behavior | Layer | Sense |
|---|---|---|
| `read file.txt` → a `file`; `read http://…` → a `url`; unknown local → generic `file` | goal | green |
| content-kind inference: `.json` content narrows to `dict`, `.csv` → table/list, unknown → `binary` | goal | green |
| `%x!file!path%` resolves without reading; `%x.field%` reads + parses + **narrows** the value | C# / int(2) | green |
| a reference **accumulates** its content type on examination — `is file` AND `is dict` both true after navigation | C# | green |
| `%x!type%` = headline (`dict` post-narrow); `%x!type.list%` = the chain `[dict, file, item]`, newest at index 0 | C# | green |
| `%config!file%` resolves on **both** the narrowed and un-narrowed branch (chain-wide `!`, not headline-only) — no flow-dependent crash | goal | green |
| `directory.list : list<path>`; `read` a child path → its content | goal | green |
| `write out %dir%` → a listing of paths/names, **not** file contents | int(3) | green |
| `write out %file%` (un-narrowed) → raw content bytes; after a narrow → reserialised; `write out %path%` → the as-typed `_location` | goal | green |
| `text` has no `.Path` | C# | neg |
| `read %url%` fetches over http; `%url!file!host%` without fetch | C# | green |
| `set %c% = read file.txt` then `write out %c%` / scalar use still yields **content**, not `"file.txt"` | goal / int(6) | green |
| `path` no longer carries `Content`/`Source`; `path.Write` emits the private `_location` (as-typed); `path.ToString` location-only | C# | green |

### Stage 4 — per-type `Compare`
| Behavior | Layer | Sense |
|---|---|---|
| `this.Type.Rank(other)` returns the higher-ranked type (`number`>`text`, date-family>`text`) | C# | green |
| `text` ordinal case-insensitive; `number` numeric across the tower (`9<10`, not lexical) | C#/goal | green |
| `text "10"` vs `number 9` → `Greater` (numeric); both directions agree | C# / int(1) | green |
| `"5" == 5` → `Equal` | goal / int(1) | green |
| date/time/datetime/duration order; `list` lexicographic; `datetime`↔ISO-text coerces | C# | green |
| `bool`/`binary`/`choice`/`dict` equality (same-type → `Equal`/`NotEqual`) | C#/goal | green |
| anything vs `null` → `Equal`/`NotEqual` (never `Incomparable`); nulls last | C#/goal | green |
| `Compare` is sync given materialised values | C# | green |

### Stage 5 — `data.Compare`
| Behavior | Layer | Sense |
|---|---|---|
| `a.Compare(b)` returns caller-order — `Less` means `a < b` regardless of driver | C# | green |
| ranking never forces a value read | C# | green |

### Stage 6 — consumers + demolition
| Behavior | Layer | Sense |
|---|---|---|
| `if` operators / `assert` map `Comparison` per the boundary table | goal | green |
| `sort %list%` orders; `sort %files% by size` async-keys + sync order, no hang | goal / int(4) | green |
| `contains`/`indexof`/`unique` match on `Equal`; type-mismatch element → no-match, **never error** | goal | int(5)/neg |
| Pile-2 sites compare/serialize via typed methods, not raw `.Value` | C# | green |
| golden-diff `Diff` still produces the same diff trees | C# | green |
| both suites green from clean after the old mediator is deleted | C#+goal | green |

### Stage 7 — surface typing + gate
| Behavior | Layer | Sense |
|---|---|---|
| public members return PLang types (`path!absolute`→`path`, `text!length`→`number`, `list!count`→`number`) | C#/goal | green |
| the gate fails a public `item`-subtype member returning raw CLR | C# | neg |
| `IsTruthy : @bool` passes the gate; an `internal` plumbing member is untouched; gated interop accessor exempt | C# | green |
| `path` interior math moved onto the type — `path.IsUnder(root)`, `path.Kind`; raw `.Relative`/`.Extension` `internal`, `.Absolute` Authorize-gated | C# | green |

## 2. Failure matrix

| Failure mode | Detected by | Result | Layer |
|---|---|---|---|
| `if %dict% > %number%` (order, non-coercible cross-type) | boundary maps `Incomparable` → error | PLang error | goal |
| `if %dict% == %number%` (equality, non-coercible cross-type) | boundary maps `Incomparable` → error | PLang error | goal |
| `if %dict% < %dict%` (order on a no-order type) | boundary maps `NotEqual` → error | PLang error | goal |
| `sort %mixed-incomparable%` | sort surfaces `NotEqual`/`Incomparable` as error | PLang error | goal |
| public raw-string accessor / `ToRaw` on a value type | the gate (and the framework-method throw) | build error / exception | C# |
| reading a value's raw via a sync public `.Value` | there is none — compile error | compile error | C# |
| `%x == null` for any type | must **not** error | `Equal`/`NotEqual` | goal |
| `[%dict%] contains %number%` (type-mismatch element) | membership returns no-match | **false, no error** | goal |
| param resolution failure (bad scheme / unset `%var%` / convert) — guard *after* `await Param.Value()` | handler returns the typed Data error | typed error, **not** an NRE on `.Value!` | C#/goal |

Impossible-by-design (do **not** assert these fail): `text`/`number` cross-type (coerces, never errors); same-type equality of any type; a default compare doing I/O (sync by construction); membership erroring on a type-mismatch (it returns no-match — the green/neg row is the correct assertion).

## 3. New surfaces this branch introduces

### Types
- **`Comparison`** enum — `app.data`, `{Less,Equal,Greater,NotEqual,Incomparable}`. New.
- **`file`, `directory`, `url`** — new `path` subtypes (`directory.list : list<path>`); `image` becomes a `file` specialisation.
- **Deleted:** `IEquatableValue`, `IOrderableValue` (Stage 6); the static `app.data.Compare` mediator, `ScalarComparer`, `Operator.NormalizeTypes`.

### Methods / members
- `Data`: `public ValueTask<object?> Value()` (the door, **new**); `Peek()` (**renamed** from `ScalarValue`); `public ValueTask<Comparison> Compare(Data other)` (**new**); a private backing read; **removed** public `.Value` property; **removed** generic `item.ToRaw()`; golden-diff `Compare` → **`Diff`**.
- type entity: `Rank(Data other)` → driving type (**new**, static rank per type); routes to the family `Compare` via the existing name→family path.
- per-type: unified `Compare` → `Comparison` (replaces `AreEqual`/`Order`); `text.Value` public-raw → **private**.
- the `.`/`!` navigation resolver (data plane vs property plane, `!` resolved **chain-wide**); references narrow on examination (identity accumulates `item|file|dict`, same `Data` instance); `!type` (headline) / `!type.list` (the chain).
- `Action.GetParameter<T>(name) → Data<T>` (**new**, generic, **lazy** — collapses the getter's `__ResolveData(name).As<T>(Context)`; the generated `__ResolveData` wrapper is removed). The incumbent is the non-generic `GetParameter(name, context)` (`action/this.cs:220`).
- navigation chain → **`ValueTask`-async**: `Data.GetChild` (`this.Navigation.cs`), `Variable.Get`/`Variable.Resolve` (`variable/list/this.cs`) were sync — now `ValueTask` (sync-completing in memory; await only the first content read). `Data.Value()` is the async door.

### The gate (Stage 7)
- PLNG-style build gate: a **public** member of an `item.@this` subtype returning raw CLR → error (warning during migration). Internal/private untouched; `IsTruthy : @bool`; engine plumbing `internal`; only exemption = gated per-type interop accessor (`path.Absolute` after `Authorize`).

### Existing surfaces touched by reference
- `PLang/app/module/condition/Operator.cs` (registry → `Compare`; `NormalizeTypes` deleted), `assert/code/Default.cs`, `list/sort.cs`/`contains.cs`/`indexof.cs`/`unique.cs`.
- `PLang/app/data/Compare.cs`, `ScalarComparer.cs` — deleted.
- `Data.MaterializeCount` (existing probe) — used by the lazy-read cut.
- The `Data`-receiver `.Value` reads across `PLang/` — migrated to `await Value()` (per-receiver, not the full 990). The ~42 handler `param.Value!` sites migrate **await → guard → use** (`var p = await X.Value(); if (!X.Success) return X; … p`), not just the `.Value` → `await .Value()` swap.
- `PLang.Generators` (Emission/Property/Data, Emission/Action) — the lazy param getter + `GetParameter<T>`; the `__ResolveData` wrapper emission removed.
- `PLang.Tests/App/DataTests/DataCompareTests.cs` — `.Compare(` → `.Diff(` (~14 sites).
