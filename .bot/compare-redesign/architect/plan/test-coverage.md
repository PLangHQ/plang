# Test coverage — comparison redesign

Three sections: the coverage matrix (one test per row), the failure matrix (negative paths), and the new-surfaces inventory. Coder reads this while implementing per-stage tests; test-designer reads it to write the suite.

## 1. Coverage matrix

Organised by stage. Layer: **C#** (TUnit), **goal** (`.goal` under `Tests/`), **int** (integration cut from `test-strategy.md`). Sense: **green** (does the right thing) / **neg** (fails correctly — cross-ref the failure matrix).

### Stage 1 — `Comparison` enum
| Behavior | Layer | Sense |
|---|---|---|
| The enum has exactly `{Less, Equal, Greater, NotEqual, Incomparable}`, no numeric backing relied on for sign | C# | green |

### Stage 2 — value door
| Behavior | Layer | Sense |
|---|---|---|
| `await data.Value()` returns the parsed value for an authored scalar (`set %x% = 5` → `5`) | C# | green |
| authored value / sync-factory / `_raw`-parse sources complete **synchronously** through `Value()`; a file/http/`ILoadable` source loads **async** on first touch | C# | green |
| a subclass that loads specially participates via the source / protected `Load()` hook (the `virtual Value` override seam moved here) | C# | green |
| view `.Value` returns the **present** value synchronously (no await, no I/O); throws if read on a pending value | C# | green / neg |
| `Value()` completes **synchronously** when the value is present (`IsCompleted` true, no async hop) | C# | green |
| `Value()` on a pending (lazy file) Data loads on first await; `MaterializeCount` goes 0→1; second await stays 1 | C# / int (cut 2) | green |
| nothing is read before the first `Value()` await (path held) | int (cut 2) | green |
| `Peek()` returns the unparsed form — a json-text raw stays the string, never a built `dict` | C# | green |
| `Peek()` on a pending value returns nothing (no load, no throw) | C# | neg |
| `ToString()` returns the present value when loaded | C# | green |
| `ToString()` on a pending value returns `<text pending>` (no I/O, no throw) | C# | neg |
| `GetHashCode` / `Equals` / operators on a view throw with guidance | C# | neg |
| `internal PresentValue()` throws on a pending value | C# | neg |
| value slot holds raw CLR — `%x%` keyed in a dict / put in a set works (keys on the raw value, not the view) | goal | green |

### Stage 3 — per-type compare
| Behavior | Layer | Sense |
|---|---|---|
| `this.Type.Rank(other)` returns the higher-ranked type (`number` over `text`, date-family over `text`) | C# | green |
| `text` vs `text` orders ordinal case-insensitive (`"a" < "b"`, `"abc" == "ABC"`) | C# / goal | green |
| `number` vs `number` orders numerically across the tower (`9 < 10`, not lexical) | C# / goal | green |
| `text "10"` vs `number 9` → `Greater` (numeric, driver = number); both directions agree | C# / int (cut 1) | green |
| `"5" == 5` → `Equal` | goal / int (cut 1) | green |
| `date` vs `date`, `time`, `datetime`, `duration` order correctly | C# / goal | green |
| `datetime` vs ISO-text coerces and compares (both directions agree) | C# | green |
| `list` vs `list` orders lexicographically | C# | green |
| `bool`/`binary`/`choice`/`dict` equality works for same-type pairs (`Equal`/`NotEqual`) | C# / goal | green |
| anything vs `null` → `Equal`/`NotEqual` (never `Incomparable`) | C# / goal | green |
| `nulls last` in ordering | C# | green |
| `Order(a,b)` is sync (no I/O) given materialised values | C# | green |

### Stage 4 — `data.Compare`
| Behavior | Layer | Sense |
|---|---|---|
| `a.Compare(b)` returns the result in **caller order** — `Less` means `a < b` regardless of which type drives | C# | green |
| ranking never forces a value read (Rank decided from types; values awaited after) | C# | green |

### Stage 5 — consumers
| Behavior | Layer | Sense |
|---|---|---|
| `if %a% > %b%` / `<` / `>=` / `<=` map `Comparison` correctly | goal | green |
| `if %a% == %b%` / `!=` map correctly | goal | green |
| `assert.equals` / `notEquals` / `greaterThan` / `lessThan` / `contains` / `notContains` via `Compare` | goal | green |
| `sort %list%` (default key) orders correctly | goal | green |
| `sort %files% by size` — async key materialise + sync order, correct result, no hang | int (cut 3) | green |
| `list.contains` / `indexof` find by `Equal`; `unique` dedupes by `Equal` | goal | green |
| membership on a type-mismatched element returns **no match** (false / distinct), **never errors** — `[%dict%] contains %number%` → false; `unique` over a mixed list keeps all | goal | neg |

### Stage 6 — demolition
| Behavior | Layer | Sense |
|---|---|---|
| golden-diff `Diff` still produces the same diff trees (renamed `DataCompareTests` pass) | C# | green |
| both suites green from a clean build after deletions | C# + goal | green |

## 2. Failure matrix

Each row is a way the system should fail — hard, typed, at the right layer.

| Failure mode | Detected by | Result type | Layer |
|---|---|---|---|
| `if %dict% > %number%` (order, non-coercible cross-type) | boundary maps `Incomparable` → error | PLang error at operator boundary | goal |
| `if %dict% == %number%` (equality, non-coercible cross-type) | boundary maps `Incomparable` → error | PLang error | goal |
| `if %dict% < %dict%` (order on a no-order type) | boundary maps `NotEqual` → error | PLang error | goal |
| `sort %mixed-incomparable%` | sort surfaces `NotEqual`/`Incomparable` between keys as error | PLang error | goal |
| `GetHashCode` / `Equals` / operator / implicit-conversion on a view | the view throws | exception, message points at `await Value()` | C# |
| `PresentValue()` on a pending value | throws | exception | C# |
| `Peek()` on a pending value | returns nothing — **not** an error (by design) | no throw, empty/null | C# |
| `%x% == null` for any type | must **not** error | `Equal`/`NotEqual` | goal |

Impossible-by-design (do **not** write tests asserting these fail): `text`/`number` cross-type comparison (it coerces, never errors); same-type equality of any type (always `Equal`/`NotEqual`); a default type compare doing I/O (default compares are sync by construction — if one isn't, that's a Stage-5 design break to fix, not a test to write); **membership (`contains`/`in`/`indexof`/`unique`) erroring on a type-mismatched element** — it returns no-match, never errors (the green/neg row in the matrix is the correct assertion, not a failure-path test).

## 3. New surfaces this branch introduces

### Interfaces and types
- **`Comparison`** enum — `app.data`, `{Less, Equal, Greater, NotEqual, Incomparable}`. New.
- **Deleted:** `app.data.IEquatableValue`, `app.data.IOrderableValue` (Stage 6). Possibly `app.data.ITextCoercible` (its role folds into per-type `Order`).

### New / changed methods on existing types
- **The async value source (net-new — Stage 2 Part A):** a source abstraction the door awaits, into which `_valueFactory` (sync), `_raw`+`Materialize()` (sync), and per-type `ILoadable.LoadAsync()` collapse. Plus a protected `Load()`/`LoadCore()` hook as the new override seam (replacing the `virtual Value` override at `this.cs:1566`). Shape is the coder's to settle; this is the largest single piece.
- `Data` (`PLang/app/data/this.cs`):
  - `public ValueTask<object?> Value()` — **new**, the single public value door (lazy, sync-complete when present).
  - `Peek()` — **renamed** from `ScalarValue` (sync, no-parse read).
  - `public ValueTask<Comparison> Compare(Data other)` — **new**, the comparison entry.
  - `internal PresentValue()` (or equivalent) — **new**, sync read of an already-present value, throws if pending.
  - **Removed:** the public `.Value` property. (Migration touches only the **`Data`-receiver** `.Value` reads — *not* the ~990 raw grep; the view `.Value` and `Lazy`/`KeyValuePair`/`Nullable`/`JsonElement` hits stay.)
  - `Diff` — **renamed** from the golden-diff `Compare` (`this.Compare.cs` → `this.Diff.cs`).
- per-type views (`text`/`number`/etc.):
  - **keep** a sync `.Value` (the present-value read; views run post-materialisation) — this is *not* removed.
  - `GetHashCode`/`Equals`/operators — **changed to throw**, shipped per type together with that type's raw-flip (live keying: `TString.cs:104,109`, `choice/this.cs`).
  - `ToString` — **changed to degrade** to `<text pending>`.
- type entity (`PLang/app/type/this.cs`):
  - `Rank(Data other)` → driving type — **new** (rank lives here, never on Data).
  - `Order(a, b)` → `Comparison` — **new**, routes to the family compare through the existing name→family path.
- per-type views (`PLang/app/type/<name>/this.cs`, all 12): a rank value, coerce-into-my-kind, sync `Order`, constructor flipped to view-over-Data (`text(data)`). **New/changed per type.**

### New PLang actions
- **None.** No new action names. Existing actions change behaviour (`condition.*`, `assert.*`, `list.sort`/`contains`/`indexof`/`unique`).

### New registrations
- **None.** Comparison reuses the existing name→family routing (`App.Type[Name].ClrType` → family behaviour, the same path as `type.Convert`). No new registry, no MIME/format registration.

### Existing surfaces this branch touches by reference
- `PLang/app/module/condition/Operator.cs` — registry wired to `Compare`; `NormalizeTypes`/`IsTextLike`/`IsNumberLike` deleted (Stage 6).
- `PLang/app/module/condition/code/Default.cs` — the `if`/`elseif` evaluator path.
- `PLang/app/module/assert/code/Default.cs` — assert handlers.
- `PLang/app/module/list/sort.cs`, `contains.cs`, `indexof.cs`, `unique.cs`.
- `PLang/app/data/Compare.cs`, `ScalarComparer.cs` — deleted (Stage 6).
- The ~990 `.Value` read sites across `PLang/` — migrated to `await Value()` (Stage 2).
- `data.MaterializeCount` (existing internal probe) — used by the lazy-read test (cut 2).
- `PLang.Tests/App/DataTests/DataCompareTests.cs` — ~14 `.Compare(` sites updated to `.Diff(`.
