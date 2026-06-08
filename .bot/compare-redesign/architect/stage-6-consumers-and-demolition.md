# Stage 6: Move the consumers onto `Compare`, then demolish the old path

**Goal:** Route every comparison consumer through `data.Compare(other)` + the boundary mapping, land the two-phase async `sort`, convert the decompose sites — then delete the old comparison machinery and rename the golden-diff.
**Scope:** Condition operators, `assert`, `sort`, list ops; the Pile-2 decompose conversions; deletion of the old mediator/coercion/interfaces; `Compare` → `Diff` rename. Closes the 2–6 green unit (both suites green at this stage's exit).
**Deliverables:**
- **condition operators** (`PLang/app/module/condition/Operator.cs`): `==`/`!=`/`<`/`>`/`<=`/`>=` and the element side of `contains`/`in` call `await left.Compare(right)` and map the `Comparison` per Stage 1's table. (The registry is already `Func<…, Task<bool>>`.)
- **assert** (`PLang/app/module/assert/code/Default.cs`): `Equals`/`NotEquals`/`GreaterThan`/`LessThan`/`Contains`/`NotContains` await `Compare`.
- **sort** (`PLang/app/module/list/sort.cs`): two-phase (below); drop `Comparer<object>.Default`.
- **list ops** (`contains.cs`/`indexof.cs`/`unique.cs`): await `Compare` per element; **match only on `Equal`, never error**.
- **Pile-2 conversions**: the ~22–30 decompose sites (`x.Value is string` → `is text`, `(string)x.Value` → typed method) become typed-method calls — **growing a type's surface where a method is missing, no `ToRaw` escape**.
- **Demolition**: delete `app.data.Compare` (static), `ScalarComparer`, `Operator.NormalizeTypes` (+ `IsTextLike`/`IsNumberLike`), `IEquatableValue`/`IOrderableValue` and the old per-type `AreEqual`/`Order`. Rename golden-diff `data.Compare` → `Diff` (`this.Compare.cs` → `this.Diff.cs`; ~14 test call sites, no production callers).
**Dependencies:** Stage 5 (`data.Compare`). Old path may coexist until the deletions here.

## Design

**Boundary mapping is the contract** (Stage 1's table): each operator turns a `Comparison` into its result, and `NotEqual`/`Incomparable` into errors — except **membership, which never errors** (`contains`/`in`/`indexof`/`unique` match on `Equal`, treat `NotEqual`/`Incomparable` as "not this one"). This is where "the value never throws" is honoured: the result is a value; the *operator* decides error-or-result.

**Sort is two-phase — the no-`GetResult` shape:**
```csharp
// PHASE 1 — materialise keys. ASYNC. All I/O here (sort by size → await file!size).
var keyed = new List<(object key, Data item)>();
foreach (var item in items) keyed.Add((await KeyOf(item), item));
// PHASE 2 — order. SYNC. Keys already in memory.
keyed.Sort((x, y) => ToInt(CompareKeys(x.key, y.key)));   // sync comparator, no await inside
```
No `await` inside the comparator ⇒ no `GetAwaiter().GetResult()`. Default compares stay sync (`file`/`path` order by name); I/O-bearing comparisons are written `sort by <key>` so the read lands in phase 1.

**Pile-2 conversions have no escape hatch.** A site that took `.Value` as a CLR primitive becomes a typed-method call; if the method is missing on the type, **add it** (that's the OBP completion). The thrown framework methods (Stage 2/7 gate) surface every remaining site loudly, so you work a list, not a grep.

**Demolish last, by deletion-and-compile.** Once consumers are on `Compare`, the old mediator/`ScalarComparer`/`NormalizeTypes` have no callers — delete, build, fix the named fallout, repeat. Remove `IEquatableValue`/`IOrderableValue` from the value types (their `Compare` replaces `AreEqual`/`Order`). Rename golden-diff `Compare` → `Diff` first (frees the name, touches only the diff + its tests). When both suites are green from a clean build, the comparison + value model is in: per-type compare over typed values, no static mediator, no `Type.Name` switch, no `_raw`, no public sync `.Value`.
