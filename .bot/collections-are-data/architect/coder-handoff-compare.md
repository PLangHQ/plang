# Architect handoff — compare lives on the value; list is orderable and nests

**To:** coder · **From:** architect · Re: the `Compare.AreEqualValues` / `Compare.Order` type-switch **and** list semantics (supersedes `list-rope-model.md`)

**You own the final shape.** Interface names, file placement, and signatures below are anchors — change what reads wrong, keep the dispositions and the recursion contract.

## Why

v2/v3 unified every membership/order site onto one path — `indexof`, `contains` (list action + operator), `in`, `remove`, `unique`, `sort` all call `Compare` now. Good. But the path itself still **re-derives the type with `is`-switches**: `Compare.AreEqualValues` has `if (lv is dict ...)` / `if (lv is list ...)`, `Compare.Order` has an `lf switch` over `Family()`, and a separate `Orderable` HashSet decides who can be ordered. That's the OBP smell — asking *what a value is* to pick behavior, in a central helper, when the behavior belongs on the value. Symptom you flagged: **`number` owns ordering (via `CompareTo`) but not equality (`NormalizeTypes` flattens it to a boxed `decimal`, then `decimal.Equals`)** — the same logical question answered two different ways depending on the operation. Scattered four ways across dict/list/number/text.

This handoff moves the per-type compare behavior onto the types and leaves exactly one legal `is`-site.

## The law

A type-discriminating `is`-switch is allowed in exactly **two** places: the **binary-coercion mediator** (`"5" == 5`, numeric widening — it inspects the *relationship* between two types, can't live on one value) and **one shared C# scalar comparer** (the leaf that compares two raw C# scalars). Everywhere else, comparison is a virtual member on the value.

## The shape — three layers

**1. Mediator (`Compare`, thins to plumbing).** Keeps only:
- null policy (both null = equal; null sorts last)
- coercion (`Operator.NormalizeTypes` — stays exactly as is)
- dispatch: *does the value implement the compare interface? → delegate. Else → the shared scalar comparer.*

No `is dict` / `is list` / `Family()` / `Orderable`-set here anymore.

**2. Collections own structural compare.** Two interfaces in `app/data/` (mirroring `IBooleanResolvable`):
- `IEquatableValue { bool AreEqual(object? other); }`
- `IOrderableValue { int Order(object? other); }`

- `dict` implements **`IEquatableValue`** only — structural, key-based (the current `is dict` arm moves here verbatim). **Not** `IOrderableValue`: dict equality is order-insensitive (`{a:1,b:2} == {b:2,a:1}`), so no positional order is consistent with it → equality-only, and `Compare.Order` throws NotOrderable for a dict exactly as today.
- `list` implements **`IEquatableValue`** (structural, positional — the current `is list` arm) **and `IOrderableValue`** (lexicographic) — see the list section below.

**Recursion contract (important):** `dict.AreEqual` / `list.AreEqual` compare each child by calling **back into the mediator** (`Compare.AreEqualValues`), not a direct `.Equals` — so a number nested in a dict still widens and text still compares case-insensitive. The collection owns *how to walk itself*; the leaves still route through the one path.

**3. One shared C# scalar comparer — the only `is`-site.** All scalars (`number`, `string`/text, `DateTime`/datetime, `TimeSpan`/duration, `bool`): the `lf switch` ordering arms and the `string`-ignore-case / `.Equals` equality tail move here. `number.@this` is already `IComparable`/`IEquatable`, so it falls through the generic arm — no special-casing. **This kills the number smell**: equality and ordering for number now live in the *same* place (here), so it owns both-or-neither consistently (neither — C# does both, once).

Net deletions: `Compare.Family()` and the `Orderable` HashSet both go — orderability becomes "implements `IOrderableValue`, or the scalar comparer says its CLR type is comparable."

## Leaf-trace — what each current site becomes

| Site (`app/data/Compare.cs`) | Disposition |
|---|---|
| `AreEqualValues` `is dict` arm | → `dict.AreEqual` (recurse via mediator) |
| `AreEqualValues` `is list` arm | → `list.AreEqual` (recurse via mediator) |
| `AreEqualValues` scalar tail (`NormalizeTypes` + string-ignorecase + `.Equals`) | → shared scalar comparer |
| `Order` `lf switch` arms (number/text/datetime/duration) | → shared scalar comparer |
| `Order` `Orderable.Contains(lf)` gate | → "implements `IOrderableValue`?" |
| `Family()` | **delete** (error messages use the value's type name / a small helper) |
| `NormalizeTypes` | **unchanged** (coercion mediator) |

**Consumers don't change.** `indexof`/`contains`/`in`/`remove`/`unique`/`sort` and `list.SortByValue/SortByField` already call `Compare.AreEqual`/`Compare.Order` — the refactor is entirely behind the mediator surface. Confirm none of them reach past it after the change.

## List — orderable lexicographically, and it nests (the rope/flatten model is rejected)

Two things settled here, and they're linked: **list ordering requires list nesting.**

**`list` IS orderable — lexicographic.** Implement `list : IOrderableValue` (Ingi confirmed). Contract:
- compare element `i` vs `i` via `Compare.Order`; first differing index decides
- one list a prefix of the other → shorter sorts first (count only as the prefix tie-break — **not** count-based otherwise: `[1,2,3] < [9]` because `1 < 9`)
- elements must be mutually orderable, else the `NotOrderable` throw propagates (same as a scalar mixed-type list)

Standard (Python/Ruby/Rust/Haskell), and trichotomy-consistent with list's positional equality.

**`list` nests like every other type — keep the flat `List<Data>`.** A list element can be a `list.@this`; `add %listB% to %listA%` adds listB as **one** element (count +1) — which is already the current behavior (`list/add.cs` does `list.Add(Value)`). **Do not implement the rope/flatten model** in `list-rope-model.md` — it's superseded by this handoff. Reasons:

- It singles out `list` as the one type that *can't* be a list element (its enumerator merges list-chunks but yields dict/matrix/table chunks whole). Every other type nests — that asymmetry is the bug, and no mainstream language has it.
- **Orderability needs nesting.** The killer use case for lexicographic list order is sorting rows / coordinates / tuples — i.e. a list *of lists*. Flattening kills exactly that.
- Its motivations don't require flattening: O(1) append is already what `List<Data>.Add` does (it never rewrites existing leaves — the rope's chunks only existed to make a *merge* O(1), and there is no merge); `count`/`foreach`/`print` already agree under honest nesting (a nested list is one element → count 1, one iteration, prints `[...]`); the "count 2 vs print 5" footgun was a symptom of *half*-flattening, which honest nesting removes. The wire-shape concern (don't envelope bare scalars) is independent and stays as the F5 / "type-driven nested Data" todo.
- Rejecting it is *less* work — don't change `add`, don't build chunks. Just add `IOrderableValue`.

If a merge ("flatten listB's elements into listA") is ever wanted, it's a **separate verb** (`concat`/`addRange`), never a silent behavior of `add` — `add` adds exactly one element, whatever its type. `matrix`/`table` stay available as specialized rectangular/headered types; they're a *choice* for column semantics, not a forced detour because lists can't nest.

## Not in this handoff

- The `value`+`type` wire-shape discriminator ambiguity you flagged (`{value:9.99, type:"book"}`) — separate, Ingi's call, cross-refs codeanalyzer F5 + the todo.
- Promoting scalars (text/datetime/duration/bool) to flow as their wrappers so the shared scalar comparer shrinks further — that's the **`scalars-as-native`** branch (planned, off main after this merges). The shared scalar comparer you build here is forward-compatible: that branch relocates its arms onto the wrappers, the mediator is unchanged.

Re-run both suites; hand back to codeanalyzer.
