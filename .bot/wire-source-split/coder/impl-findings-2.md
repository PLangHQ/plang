# Coder impl-findings 2 — the write rule is not sufficient (Peek consumers leak)

Branch: `wire-source-split`. Follows the 2026-07-12 ruling (uniform write-time rule + all-wires).
I implemented it (`ISerializer.Owns(IWriter)` default-false, transport overrides
`writer.Format is "plang" or "json"`, `wire.Write` = verbatim-if-owned-else-materialize). It
compiles and is correct for the WRITE path. **But it does not fix the quote-leak**, because the
leak is also on the READ path, through `Peek()` — which the write rule never touches.

## The trace (root, not symptom)

`StartGoal_Programmatic` still outputs `NewVar: "Plang"` (quoted); `ResolveValue_StringInterpolation`
still yields `Hello "World"!`. Both resolve `%var%` through string interpolation:

```
text.@this render  → context.Variable.Resolve(_value)          (text/this.cs:126)
Variable.Resolve   → s = dataVar?.Peek()?.ToString();          (variable/list/this.cs:482)
```

`Resolve` renders each `%x%` from the variable's **Peek()** — the raw form — *by design*
(its own comment: *"a bare `%x%` renders the value's raw source form via Peek()… `%cfg%` of a
lazily-read config.json is the raw json string"*).

- A text **content source**'s Peek is the decoded content → `Plang`. (baseline)
- A text **wire**'s Peek is the captured document slice → `"Plang"` (quotes + escapes).

So under all-wires, every Peek-based consumer of a text literal gets the quoted slice. Interpolation
is one; the **OnSet/OnCreate change-tracking events** (`variable/list/this.cs:164-261`, all Peek) and
any display/`ToString` path are others. This is precisely the plan's flagged "Peek consumer audit",
and it is **wide** — not a one-line fix, and not addressed by the write rule.

## Why the write rule can't reach it

`Owns(IWriter)` is a *write*-time decision (which encoder is receiving the slice). Peek is a *read*
that returns a value's in-memory raw form with no writer in sight. There is no writer to ask. To make
Peek return decoded content for a text wire, the wire would have to materialize inside Peek (breaks
laziness) or carry face knowledge ("I'm text, unescape my slice") — the exact face knowledge the
all-wires design set out to remove.

## The tension interpolation actually encodes

`%x%` interpolation wants two different things by shape, and the Peek-based code conflated them
(safely, while content-sources decoded scalars):

- **scalar `%name%`** → the value's **content** (`Plang`, `42`).
- **structured `%cfg%`** → the value's **raw json** (`{...}`) — deliberately not materialized.

All-wires breaks the scalar half: a text scalar's Peek is now quoted json, not content.

## Recommendation — reopen the fork toward Option A (text-faced → content)

Option A resolves the WHOLE class in one stroke, not just writes:

- text/datetime/guid/path string tokens → **content source** ⇒ Peek returns decoded content ⇒
  interpolation, events, display all read `Plang` with zero consumer changes.
- `%cfg%` structured stays a **wire** ⇒ Peek returns raw json ⇒ its intended behaviour survives.
- number/bool/structured string-token mismatches stay strict wires (`"23"`/{number} fails at first
  touch) ⇒ **Ingi's strictness ruling is preserved** — the carve-out is only "a string token is
  content when the declared type's value is itself a string."

The `Owns`/uniform-write-rule work stays useful regardless (it keeps a structured wire byte-identical
on relay and materialized on a foreign writer). Option A just moves text scalars off the wire so their
raw form is content again.

If instead all-wires is held, the branch must include a **Peek-consumer audit**: interpolation
(`:482`), OnSet/OnCreate (`:164-261`), and every `ToString`/display of an unmaterialized value must
switch to a materialize-for-scalar path — a larger, more fragile change that also has to re-derive the
scalar-vs-structured distinction Option A gets for free from the declaration.

## Ask

Reopen the fork: **Option A** (recommended — clean, preserves strictness), or hold all-wires and
authorize the Peek-consumer audit? I'm proceeding with issue-1 (object/json add-that-delegates) in
parallel since it's independent and clears the other 3 remaining reds (dict round-trips, config-json).

— coder
