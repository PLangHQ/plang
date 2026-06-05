# Coder — collections-are-data — v3

**Resolves architect handoff** `coder-handoff-decompose.md` (`c4acecfd6`). Items A, B, D
(C was = codeanalyzer F4, already done in v2). Both suites green from clean rebuild:
**C# 4082/0**, **plang 273/273**. Build clean, 0 errors.

## A — wire-reconstruction navigates the native dict, no deep decompose (resolved)

`app/data/this.cs`. `AsCanonical` bound every list/dict-shaped variable through
`IsWireShape` → `AsRawWireDict` → `nd.ToRaw()` — a recursive deep decompose of the whole
dict — only to read two keys, paid even when the dict turned out *not* wire-shaped, and
throwing away the Data-keying this branch exists to preserve (nested non-wire values were
flattened with their type-tags gone).

Replaced the flatten-then-read with native navigation:
- `WireSlot(raw, key)` / `HasWireKey(raw, key)` read a single slot from a native dict
  (`Get`/`Has`) or a raw dict — never `ToRaw`. Only the `value`/`type` slots are touched,
  so unrelated nested values keep their native Data-keying.
- `IsWireShape`, `FromWireShape`, `TypeFromWire` rewritten to navigate; `TypeFromWire` reads
  the structured `{name,kind,strict}` type via `Get` too (no per-type-dict `ToRaw`).
- Deleted `AsRawWireDict` (now unused).

Behavior-preserving: same `value`+`type` discriminator, same reconstruction. The win is
hot-path (no deep decompose on every bind) + correctness (nested non-wire values no longer
flattened). Signing/snapshot round-trip suites stay green.

**Open design question, NOT changed here** (architect A.45 = cousin of codeanalyzer F5):
the discriminator still keys on `value`+`type` presence, so an ordinary user dict like
`{value: 9.99, type: "book"}` is mis-detected as a serialized Data on the bind path. Picking
a non-ambiguous discriminator (reserve a marker, key on `signature`, or a `data` type stamp)
is a language-semantics decision that spans both this site and F5 — flagged for Ingi, not
decided unilaterally. (Note: the `data`-type-stamp endgame is already a tracked todo in
`Documentation/Runtime2/todos.md` "Fully type-driven nested Data".)

## B — `dict.ToRaw()` decomposes nested native lists (resolved — real asymmetry bug)

`type/dict/this.cs`. `Unwrap`'s switch handled nested dicts but `list.@this` (not
`IEnumerable`) fell through to `_ => value` and survived un-decomposed inside the
supposedly-raw `Dictionary<string,object?>` — breaking domain-record reconstruction at the
`Conversion.cs` leaf for any record with a `List<T>` fed from a dict entry that's a native
list. Added the `app.type.list.@this nestedList => nestedList.ToRaw()` arm (symmetric with
`list.ToRaw`'s nested-dict arm) and replaced the stale "Stage 1 lists are still
`List<object?>`" comment.

## D — CommandLineParser array case made consistent (resolved, minor)

`app/Utils/CommandLineParser.cs`. The object case decomposed to raw (`ToRaw`) while the array
case returned a *native* list — inconsistent for an infra flag property bag. Array now also
`ToRaw`s, so both come out raw (and B's fix means a nested list inside a config object
decomposes correctly). The build-native-then-flatten round-trip remains — Ingi filed a todo
to rewrite the parser as a typed `Deserialize<Build>(--build)` pass; left for that focused
perimeter cleanup, not unwound mid-branch.

Hand back to **codeanalyzer** / **architect** for re-review.
