# Test coverage — `scalars-as-native`

For the test-designer. The coverage matrix (surface × stage × layer), the failure/mutation matrix (what a missing test lets through), and the new-surfaces inventory.

> **You own the final shape.** The matrix is the coverage contract — names, fixtures, and exact assertions are yours.

## Coverage matrix

| Surface | Stage | Layer | Must assert |
|---|---|---|---|
| `item` universal contract | 1 | C# unit | `item` carries truthiness + the lazy narrow; `number`/`dict`/`list` are `: item`; truthiness reachable through the base |
| **`dict : item` keeps no order** | 1 | C# unit + integration | `dict` is `: item` **and** `Compare.Order(dict)` still throws `NotOrderableException` (item must not leak an order); `list : item` still sorts |
| `object` folds into `item` | 1/5 | integration | a read-but-unexamined json value is `item(kind=json)`, narrows to `dict`/`list` on touch; no PLang `object` type remains |
| `number` unchanged | 1 | integration | arithmetic, compare, `if`, `→ returns int`/`decimal` identical to before |
| `text` ops | 2 | C# unit | length, case, contains, substring, split, trim |
| `text` value-equality (HashSet / list-element) | 2 | C# unit | two `text("a")` equal + hash-equal; **a `text("a")` and a raw `string "a"` resolve consistently in a `HashSet` / `list`-element dedup** (mid-migration aliasing guard — the implicit operator compiles but does not hash-equal; coder #3). Note: dict *keys* are string-indexed, so the hazard is element/value equality, not keys |
| `text` truthiness | 2 | C# unit | empty falsy, non-empty truthy |
| `text` atomicity | 2 | integration | `foreach %s%` does **not** iterate characters |
| `text` born native | 2 | integration | `set %s%="x"` → wrapper; `→ returns string` reconstructs |
| `text` serialize | 2 | integration | bare `"x"` on `.json`; signed text survives `.plang` |
| **`date` ≠ `datetime`** | 3 | integration | **`%d% is date` true, `is datetime` false** (load-bearing) |
| `time` is its own type | 3 | integration | a time value is `time`, compares/sorts as time (was unhandled) |
| `datetime` accepts `DateTime` | 3 | C# unit | CLR `DateTime` input → `datetime.@this` |
| datetime/date/time compare | 3 | C# unit | each orders within its type; ISO serialize; `→ returns` each CLR type |
| `duration` parts + compare | 4 | C# unit | parts, order, equal-durations value-equal; `→ returns TimeSpan` |
| `duration` truthiness | 4 | C# unit | documented zero-vs-nonzero policy |
| `bool` truthiness primitive | 5 | C# unit | wraps raw `bool`; `AsBooleanAsync` bottoms out at it |
| `bool` in a condition | 5 | integration | `if %b%`, `if !%b%`, `assert.IsTrue` read a `bool.@this` result |
| `bool` born native | 5 | integration | JSON `true`/`false` → `bool.@this`; bare on `.json` |
| `null` singleton + truthiness | 6 | C# unit | singleton instance; always falsy; `null==null`; **sorts last** (the sort places nulls last — `null` isn't self-orderable, doesn't implement `IOrderableValue`) |
| **`null` value vs absent Data** | 6 | integration | `set %x%=null` → `null.@this`; **missing var → `NotFound`, not `null.@this`** |
| coercion mediator over wrappers | 7 | C# unit | `"5"==5`, numeric widening, date-vs-datetime — inspects wrappers, not raw CLR |
| `Variable : item` | 7 | C# unit | `Data<Variable>` satisfies `where T : item`; name-resolution still works |
| **`where T : item` compiles** | 7 | C# unit | no `Data<rawCLR>` survives; a `Data<int>` must **not** compile; `Data<object>` gone; the ~25 generic `Data<T>` infra methods carry the constraint |
| **double-wrap impossible** | 7 | C# unit | `Data` is not an `item`, so `Data<data.@this>` must **not** compile (structural footgun-kill) |
| `Ask`/`snapshot`/`path` `: item` | 7 | C# unit | concretely-typed `Data<Ask>`/`Data<snapshot>`/`Data<path>` compile under the constraint; the types implement no contract they can't honor |
| `ScalarComparer` collapsed | 7 | C# unit | `Name()`/per-type switch gone; compare routes through `item` dispatch |

## Failure / mutation matrix

What each test catches — and what slips through if it's missing.

| Mutation | Caught by | Slips through without it |
|---|---|---|
| `date` still coerced to `datetime` | `%d% is date` / not `datetime` | dates compare/sort as datetimes; a whole type silently doesn't exist |
| `text` char-iterates | `foreach %s%` atomicity | a string value explodes into chars in a loop |
| wrapper equality is reference, not value | `text("a")` in a `HashSet`/list-element dedup | dedup/`HashSet`/element lookups silently miss |
| **`item` leaks an order onto `dict`** | `dict : item` + `Compare.Order(dict)` throws | `dict` claims an order it can't honor → wrong sort results, the contract `collections-are-data` audited regresses |
| `null.@this` swallows absent-Data | missing-var-is-`NotFound` guard | "variable not set" becomes "variable is null" — masks real errors |
| construction leaves a raw scalar | born-native test per type + constraint compile | `value is string` body sites go silently false mid-flight |
| signature left `Data<rawCLR>` | `where T : item` compiles | the slot keeps a raw type; double-wrap footgun stays reachable |
| mediator reads raw CLR | `"5"==5` after the sweep | cross-type coercion breaks once values are wrappers |
| bare-vs-wire serialize confused | `.json` bare / `.plang` signed per type | signatures leak into `.json`, or `.plang` loses them |
| `bool` truthiness regressed | `if %b%` / condition-action test | conditions misread; the most load-bearing runtime path |

Mutation-test the two load-bearing ones explicitly (announce per CLAUDE.md): (1) after Stage 3, revert the `ScalarComparer` date fix → the `is date` test must go red; (2) after Stage 7, reintroduce one raw-scalar construction arm → a compile error or red test.

## New surfaces inventory

Surfaces that don't exist yet and need first tests:

- `app/type/item/` — the apex *and* un-narrowed type (`object` folds in); carries truthiness + the lazy narrow, **not** ordering. Test: `dict : item` keeps no order; un-narrowed `item(kind=json)` narrows on touch.
- `app/type/bool/` — new wrapper (truthiness primitive, equality, serializer).
- `app/type/null/` — new **singleton** wrapper (falsy, `null==null`, sorts last, bare `null`).
- `app/type/date/` — new wrapper, `DateOnly`, **distinct from datetime**.
- `app/type/time/` — new wrapper, `TimeOnly`.
- `text`/`datetime`/`duration` — promoted from thin shells to full wrappers (ops, compare, truthiness, serializer).
- the `where T : item` constraint — negative-compilation coverage (a `Data<int>` must not build).
- the coercion mediator rewritten over wrappers — cross-type reconciliation.
