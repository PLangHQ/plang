# Plan: Context (and Actor) is never null — one Data read path

**Status:** design, for its own branch (off the current branch; come back to the cache fix + remove-goalcall after).
**Why now:** the LLM-cache double-wrap that blocks `plang build` is a *symptom* of this. It can't be fixed cleanly in isolation — every isolated attempt dead-ends on the same root cause (below). Fix the invariant, the cache fix becomes trivial.

---

## 1. The invariant

`actor.context.@this` and `actor.@this` are **never null** at runtime. Remove `Context?` / `Actor?` nullability as a reachable state. A read/serialize that has no context is a **bug to fix at the caller**, not a state to branch on. Retire the deliberate context-less escape hatch (`plang.@this.ContextLessFallback`).

## 2. Root cause this fixes (the evidence)

There are **two divergent ways a Data value is read from the wire**, and they disagree on a nested typed entry `{type:{name},value}` (no `@schema`):

| read | context | path | nested `{type,value}` entry |
|---|---|---|---|
| `.pr` load (`GoalReadOptions(context)`) | yes | `Wire.ReadBody` → **Typed reader** (dict/list) → `ReadSlot → Parse` | born to its type ✓ |
| settings / snapshot (`new plang.@this()`) | **none** | `Wire.ReadBody` → `_context!=null` gate fails → fallback `item.serializer.json.Parse` → `ObjectLeaf → RawSlot` | left a raw `{type,value}` dict ✗ |

`Wire.cs` ~406 (`&& _context != null`) is the fork. The context-less branch lands on a second, typed-entry-blind narrow (`RawSlot` checks only `IsDataMarked`, not `IsTypedEntry`). The LLM cache stores the result Data Store-view (self-describing per-entry), reads it back **context-less**, the nested `{description,steps}` come back as raw `{type,value}` dicts → `%plan.steps%` misses → foreach no-ops → the sibling `goal.call` runs once with a stale outer `%item%` → `%planStep%` is a goal → `IndexNotSet` three layers downstream.

**Two fixes that DON'T work in isolation (proven this session — don't repeat them):**
- *Teach `RawSlot` the `IsTypedEntry` check.* Breaks **fresh** LLM parsing: an LLM result legitimately contains `{type:{name},value}`-shaped objects (action parameters), and `RawSlot` runs over arbitrary LLM JSON, so it mis-borns them (`value slot 'Value' has no declared type`). The `{type,value}` collision is real and unavoidable on the context-less narrow.
- *Give `Sqlite` a context (route through the Typed reader).* Correct in spirit, but it flips at-rest Store reads to **verify signatures on read** (today trusted-because-no-context), which is +~42 Runtime/Types failures. That fallout IS this invariant work surfacing — it belongs here, not in a cache patch.

The collision is decisive: only the **context-ful Typed-reader path** can tell a wire typed-entry from look-alike user JSON, because it reads the declared *wire shape* rather than guessing from arbitrary content. So the fix is **eliminate the context-less narrow**, not patch it.

## 3. The unification

Make `Wire.ReadBody` route **every** typed value through the Typed reader, regardless of context (drop the `_context != null` gate). The context-less `Parse → RawSlot` fallback becomes dead for typed values. Then a null context at a Store read is impossible-by-construction; add the tripwire (below) to prove it.

Net end state: **one Data read** (the `.pr` Typed-reader path); `RawSlot`/`ObjectLeaf` keep only their genuine job (raw scalars / untyped containers); `ContextLessFallback` is gone.

## 4. Production sites to fix (the context-less surface)

- `settings/Sqlite.cs:20` — `_serializer = new()`. Thread the owning context (System.Context is in hand at `app.this.cs:CreateSettingsStore`). Ctor signature change touches 3 test callers (`DataSourceTests`, `AbsoluteDisciplineTests`, `SqliteAuthorizeDenialTests`).
- `channel/serializer/plang/this.cs:84` — `_snapshot` Wire built with **no context** (ignores the ctor's). Pass `context:`.
- `snapshot/this.Wire.cs:89` — `WireOptions` `?? ContextLessFallback`; `:83` `FromWire(raw,kind)` 2-arg registry seam (drops context). Fix: register a context-ful `serializer/Default.Read(raw, kind, ReadContext)` (done-pattern from dict) so `source.Value`'s reader branch carries `ctx.Context`; retire the 2-arg seam.
- `data/this.Transport.cs:57,142` — `_context.Actor? ?? ContextLessFallback` (Compress/Decompress, writes). Require actor.
- `module/crypto/code/Default.cs:22` — `ContextLessFallback` for hashing. Decide: hashing is content-only (may be genuinely context-free) vs. give it the data's context. Confirm.
- `channel/serializer/plang/this.cs:141` — **delete** `ContextLessFallback` once the above are converted.
- `app.this.cs:245` — `CurrentActor` defaults to `User` but a fresh app's `User.Context` reaches snapshot reads as null in tests; ensure a fresh app always has a non-null `CurrentActor.Context`.

## 5. The behavior change to design for (don't skip)

Giving Store reads a context means at-rest artifacts (identity, permission, llmconfig, llmcache) **verify their signature on read** instead of trusting it. This is the bulk of the +42 fallout. Decide and implement: either (a) at-rest verify is desired (more correct) and the signing keys are reliably present on the read path, or (b) at-rest stays trust-on-read but via an *explicit* Store-view trust flag, not via "context happened to be null." (a) is the honest reading of "context is never null."

## 6. The tripwire (catch regressions)

Re-add to `Wire.ReadBody` (after the no-declared-type throw): `if (_context == null && View == Store) throw` with a message naming the slot+type. Flip it on once the production sites are converted; it then guards the invariant. (It was validated this session — it cleanly catches every context-less Store typed read.)

## 7. Test-fixture sweep (the bulk of the work)

The tripwire proved ~**61** test failures across suites (Runtime ~+28, Types ~+18, Data +8, Modules +4, Wire +3 over a ~23 baseline) — almost all **fixtures** that build a context-less serializer/app and round-trip Store-view Data. Plan: a shared test helper that always supplies a context (e.g. `TestApp` + its `User.Context`); sweep the fixtures onto it. Mechanical but voluminous — budget for it explicitly.

## 8. Adjacent (track, may fold in)

4 pre-existing Wire failures (`Properties_RoundTrip_*`, `Deserialize_ShallowNesting`) — the "data.Output write-path migration tail": a value serialized as recursive `{name,value}` with **no type** that the strict reader rejects (`value slot 'a' has no declared type`). The context-ful read exposes 2 more (snapshot `PlangPath`/`ThrowTime`). Same family; decide whether this invariant work also fixes the `{name,value}` no-type serialization or tracks it separately.

## 9. Then come back

With context never null and one read path: the LLM cache fix is trivial — `settings.Set(table, key, result)` stores the whole Data, `settings.Get` reads it back faithfully (Typed reader), `Cached=true`, return. Delete `RestoreFromCache` + `ParseResultValue`-on-read + the `Messages` echo prop. Re-validate `plang build` fresh + cache-hit. Then resume `remove-goalcall`.

## What's already in hand (this session)

- **Keep:** the self-diagnosing `IndexNotSet` error (`data/this.Navigation.cs`) — reports the index root's binding (type + peek); it's what made this whole chain diagnosable. Pure improvement, no behavior change.
- **Pins:** `PLang.Tests/.../loop/ForeachGoalCallParamNavTests.cs` (foreach+goal-call+nav, passes — guards the call-by-value injection) and `PLang.Tests/Wire/.../DictTypedEntryRoundTripTests.cs` (the dict-of-typed-entries Store round-trip — currently RED context-less; it goes green when §3 lands, so it's the acceptance test for this work).
- The cache/Sqlite/snapshot/throw edits were all reverted (they belong here, not on a cache patch).
