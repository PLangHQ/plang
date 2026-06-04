# coder — lazy-deserialize — v3 report

## Status: GREEN, ready for code analyzer.

- **plang goal suite: 272 / 272 pass** (0 fail, 0 timeout, 0 stale, 0 skipped)
- **C# suite: 4021 / 0**
- Working tree clean; `origin/lazy-deserialize == HEAD` (`ead0caa83`). No diagnostic remnants.

This session closed out the goal-level LazyDeserialize tests (the v2 "10 stale" stubs are now built and green) and resolved the signing round-trip at the goal layer, which surfaced — and fixed — a class of internal **Data→JSON** round-trips.

---

## Commits this session (on top of v2 `0fcb08e0a`, all pushed)

```
ead0caa83 variable.set and list.add bind by shallow clone, not deep JSON clone
d574f67ca reconstruct a Data from its wire shape when binding to a data.@this slot
f77932633 goal-call params clone the live Data instead of JSON round-tripping
00b66da32 revert speculative goal-call signature-chasing (superseded by d574f67ca)
5b76067a8 double-default + hash-excludes-name + nav-errors + 3 negative-goal rewrites
10b4d8c6b test(NavigationOnTypeUnknown): flag-pattern rewrite
a07b8d54d fix(navigation): navigating .field into a string errors (no auto-parse)
2e83bf193 fix(signing transfer): Signature survives full-match %var% + goal.call
ba07025ab reply v2 + fixes: cache full-bypass; Signature carried across variable.set
60878a2af fix(variable.set): carry Signature across the binding-mint
4d59cd382 fix(llm cache): --build={"cache":false} fully bypasses the LLM cache
f2df3a045 stage 5 (goal-level lazy): keep lazy reads lazy through the goal pipeline
```

---

## Deliberate behavior changes (NOT regressions — please don't flag as such)

These are intentional language/semantics decisions made with Ingi this session. Each
changed C# tests to match; if the analyzer sees the old contract in history, the new
one is authoritative.

1. **Fractional literals default to `double`, not `decimal`.** Bare `1.5` → double.
   `UnwrapJsonNumber` no longer tries `decimal` first. Pinned in `DataTests`
   (`UnwrapJsonElement_FractionalNumber_DefaultsToDouble`).

2. **The signed hash excludes the variable name.** `Wire.Write` under
   `MarkOuterForHash` omits the `name` field, so a value verifies the same regardless
   of which variable holds it (`sign "x" → write %a% → verify %a%`). Pinned in
   `CanonicalizationTests`.

3. **Navigating `.field` into a string/unknown-type value errors — no auto-parse.**
   `%cfg.port%` where `%cfg%` is raw text surfaces a typed error asking for `as <type>`,
   instead of silently parsing. (`this.Navigation.cs`, broadened from unknown-type-only.)

4. **Math precision default is `Error`** (not Double/Decimal) — mixed-precision ops
   require an explicit `as`. Pinned in `NumberPolicyResolutionTests`.

5. **`variable.set` (no `as`) and `list.add` bind by shallow clone → store a plain
   `Data`, not `Data<T>`.** The PLang `.Type` entity is still inferred and correct;
   only the CLR generic wrapper is gone (behavior must not switch on `Data<T>`, per
   OBP). Reference semantics now apply to in-place mutation of a shared value object.
   `SetTypeInferenceTests` rewritten to assert `.Type.Name` + value, not `IsTypeOf<Data<T>>`.

---

## The signing round-trip fix (the substantive work) — two internal Data→JSON leaks

`SignAndVerifyRoundTrip` (`sign → write %signed% → call goal → verify %signed%`) failed
because a `Data` was being serialized to a JSON dict internally and read back wrong,
drifting its type (`text` → `object` → `dict<text,object>`) so sign and verify hashed
different canonical shapes. Two distinct sites, both fixed:

- **Goal-call params (`f77932633`).** A `%var%` param was flattened by the generic
  container walk and rebuilt as a fresh `Data`, dropping Signature/Type. Now a
  full-match `%var%` param **clones the live Data** (`ShallowClone(name)`); `GoalCall`
  is carved out of the generic walk (`IsSelfResolvingParams`) and `Convert` resolves
  each param via `data.@this.ResolveParameter` + resolves the dynamic goal-name `%var%`.

- **Wire-shape reconstruction (`d574f67ca`).** A `{value, type, …}` object **is** a
  serialized `Data`. When one reaches a `data.@this` slot — typed `Data<T>`
  (`Conversion.TryConvert`) or plain `Data` (`AsCanonical`) — it is now reconstructed
  as a whole (value + type) via `data.@this.FromWireShape` instead of being nested as
  the Data's value (which mislabelled the type `object`). `IsWireShape`/`FromWireShape`
  live once on `data.@this`; both binding paths call them.

### The follow-on cleanup (`ead0caa83`)
Auditing for "other Data→JSON that shouldn't happen" found `SnapshotClone` (a JSON
round-trip with **default** options — not the Wire converter) used to deep-clone
values in `variable.set` (List/Dict arms) and `list.add`. It flattened any nested
`Data`/domain type to a naive dict (dropping Signature, real type, `[Out]`) and
materialized the value (defeating lazy). Both now use `ShallowClone(name)`.
`ShallowClone` gained two fixes so it can be the universal bind clone: carries `_raw`
(stays lazy) and sheds a bare `object`/no-kind type so the value's real CLR type
derives (that ownership is on the clone, not per-call-site). `MintTyped` removed.

**Left intentionally unchanged:** `Variables.Set`'s dot-path clone
(`variable/list/this.cs:292`) still deep-clones — it guards a documented builder-trace
aliasing bug and is a nested-property-set into a C# object, not a binding mint.

---

## Files touched this session
- `PLang/app/data/this.cs` — `ShallowClone(name)` (+_raw, +object-shed), `IsWireShape`/`FromWireShape`/`TypeFromWire`, `IsSelfResolvingParams` carve-out, `ResolveParameter`, double-default, nav-error helpers
- `PLang/app/data/this.Navigation.cs` — navigate-into-string errors
- `PLang/app/data/Wire.cs` — hash excludes name; double-default read
- `PLang/app/type/list/Conversion.cs` — wire-shape reconstruction at the `data.@this` bind
- `PLang/app/goal/GoalCall.cs` — `Convert` uses `ResolveParameter` + resolves dynamic name
- `PLang/app/module/variable/set.cs` — shallow-bind tail; `MintTyped` removed
- `PLang/app/module/list/add.cs` — shallow clone
- `PLang/app/module/math/MathPolicy.cs` — precision default Error
- `PLang/app/module/llm/code/OpenAi.cs` — `--build={"cache":false}` full bypass
- `PLang.Tests/**` — `SetTypeInferenceTests`, `CanonicalizationTests`, `DataTests`, `NumberPolicyResolutionTests` re-pinned to the new contracts
- `Tests/LazyDeserialize/*.test.goal` + fixtures — built + green

---

## Known gap (not a blocker — flagged for the analyzer / next pass)
- **No "a `Data` inside a collection keeps its identity/signature" test.** That gap is
  exactly what let the deep-clone corruption hide for so long (every passing test read
  `.Value`/`.Type`, never the CLR generic, and never put a *signed* Data through a
  collection or goal-call). Recommended regression: `sign → add %signed% to %list% →
  verify %list[0]%` passes. Offered to Ingi; not yet written.
