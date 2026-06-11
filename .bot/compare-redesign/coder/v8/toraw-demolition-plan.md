# ToRaw demolition — ready-to-run plan (slice-2b structural)

**Status:** not started — deferred to avoid colliding with architect's in-flight
conversion/Normalize rework. Execute as one atomic pass once that lands (or on
explicit go). The key equivalence is proven, so risk is churn, not semantics.

## The proven equivalence (de-risks the whole change)

`ClrConvert(x, typeof(object))` returns `x` unchanged — `object` is assignable from
everything, so the `target.IsInstanceOfType(backing)` arm short-circuits
(`type/item/this.cs:270`). Therefore for every type:

    leaf.Clr<object>()  ≡  leaf.ToRaw()

And `item.@this.Backing(v)` (`type/item/this.cs:258`) already IS the call-site pattern
`v is item {IsLeaf:true} l ? l.Clr<object>() : v` — i.e. the `is item iv ? iv.ToRaw() : v`
arms become `Backing(v)`.

## The blocker that makes it atomic

Leaves that override ONLY `ToRaw` (not `Clr`) — number, choice, date, datetime,
duration, bool, time, absent, null — would throw "declares no CLR backing" if a
caller did `Clr<object>()` on them. So every such leaf needs a `Clr` override BEFORE
`ToRaw` is deleted. Base virtual `ToRaw` + all overrides + all callers are coupled →
one build cycle.

## Steps (one pass, build once at the end)

1. **Add `Clr` to the 9 leaves that lack it** — replace
   `internal override object? ToRaw() => Value;` with
   `internal override object? Clr(System.Type t) => ClrConvert(Value, t);`
   - number (`_value`), choice (`Value`), date, datetime, duration, bool, time (`Value`)
   - absent → `=> null;`, null → `=> null;`
   (text, binary, source, clr, dict, list already have `Clr`.)
2. **dict/list** — their `Clr` calls `ToRaw()` internally and `Unwrap` recurses via
   `leaf.ToRaw()`. Rename the raw-build (`ToRaw`) to a private `RawForm()` (the pin
   forbids the *name* ToRaw at any visibility, a differently-named private is fine),
   have `Clr => ClrConvert(RawForm(), target)`, and change `Unwrap`'s
   `leaf.ToRaw()` → `leaf.Clr<object>()`.
3. **Convert the external call sites** (`grep '\.ToRaw()'`, ~9 non-def):
   - `catalog/Conversion.cs:155,172`, `number/this.Convert.cs:22`, `type/this.cs:168`,
     `tester/this.cs:63`, `config/this.cs:94`, `variable/list/this.cs:625`,
     `dict/this.cs:154`/`list/this.cs:331` (the Unwrap arms, step 2).
   - Pattern `is item iv ? iv.ToRaw() : v` → `Backing(v)`. Bare `x.ToRaw()` → `x.Clr<object>()`.
   - **CommandLineParser.cs:139,140** — stage calls it "documented perimeter, not 2b",
     but the exit gate wants `\.ToRaw()` → 0. Either convert these two to `.Clr<object>()`
     (mechanical, equivalent) or get architect's ruling on excluding them from the gate.
     **Open point — confirm with architect.**
4. **Delete** `internal virtual object? ToRaw()` (base, `type/item/this.cs:343`) and all
   overrides.
5. **Pin** — add `GenericToRaw_DoesNotExist_OnItemBase` asserting no `ToRaw` member at
   any visibility (reflection incl. NonPublic).
6. Build; the compile errors are the remaining worklist. Then full suite — should be
   parity (equivalence proven), no behavior change.

## Exit-gate checks after
- `grep -rn '\.ToRaw()' PLang/app` → 0 (CommandLineParser per the open point above).
- `is/as` on plang types outside `type/` → only proven leaves (separate, larger).

## Why deferred now
Architect is reworking conversion + Normalize (lazy-streaming writer, template-ownership).
A 20-file ToRaw refactor through the same machinery would collide. Sequence it after,
or coordinate the merge.
