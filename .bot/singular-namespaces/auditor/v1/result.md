# Auditor v1 — result (singular-namespaces)

**Reviewing:** branch HEAD = `01d4d2d6d` (tester v3 PASS) on top of coder v3
(`a2ac0d576`). Codeanalyzer last touched at v4 (`9dbd6bce2`, reviewed coder v2).

**Method:** read all four prior bot summaries + the codeanalyzer v4 report + the
tester v3 verdict; diffed `9dbd6bce2..HEAD` to confirm coder v3 made only
test/`.pr`/`Capture.goal` changes (no new production code post-codeanalyzer v4);
spot-traced the four codeanalyzer-v4 latent findings in HEAD; traced the producer
stamping path through `Data.Context` setter → `_type.Context`; clean-rebuilt
(`dotnet build PlangConsole` → 0 errors, 254 pre-existing warnings); ran
`PLang.Tests` (**3696/3696**) and `plang --test` from `Tests/` (245 pass; **8 HTTP
transients** — `httpbin.org` flake, same shape tester reported, cleared on re-run
during my session for several but I didn't re-run the suite).

---

## Verdict: **PASS** (with one note for docs/security routing)

The codeanalyzer + tester verdicts hold up. The fundamentals (non-null
`Data.Context`/`Data.Type`, `type.Null` sentinel, `Promote()` throw, producer
stamping) are consistent across the seams; the architecture (`type/this.cs` entity
+ `type/list/this.cs` registry) matches the architect's plan; the cache-build
safety codeanalyzer v3 originally proved via "Promote short-circuits on null
Context" still holds (now via `_foldLoaded`); test green is real.

---

## Assessment of prior reviews

| Reviewer | Verdict | My take |
|---|---|---|
| codeanalyzer v4 | PASS (4 minor/latent notes) | **Agree.** F1/F3/F4 are real shape comments but not bugs today; F2 was addressed by coder v3 (test rename). |
| tester v3 | PASS (mutation-confirmed) | **Agree.** v3 mutation pairs are the right shape — delete-the-throw → exactly-the-named-test-fails. Honest green. |
| security | (not run) | See finding F1 below — recommend routing, but not blocking. |

---

## Findings

### F1 — No security review on this branch  *(minor — routing)*
**Category:** review-gap **Missed by:** none (gap in pipeline, not in a bot)
**File:** branch-wide

The branch did not run security. The functional changes touching
security-relevant surfaces are:
- `permission/this.cs:67` — `Permission.Find` producer-stamps Context onto
  SQLite-rehydrated grants (codeanalyzer v4 vetted as root-cause-correct).
- `type/this.cs:168` — `Promote()` throws with `$"type.@this(\"{Value}\") has no
  Context — ..."`. `Value` is a system-controlled type name (catalog or
  `FromName(...)` literal), not user-controlled input — low info-leak risk.
- `data/Wire.cs:239` — wire deserialization mints a new `type(typeStr)` from
  the on-wire `typeStr`. The string flows into the type's `Value` field but
  doesn't unlock new code paths beyond what was already there (Wire round-trip
  hasn't changed its threat model).

This is a refactor, not a new attack surface. Risk is low, but the convention
on this repo is that any branch touching `Permission.*` or `Wire.*` gets a
security pass. Recommend running security before docs.

**Suggestion:** route through security on the way to docs.

### F2 — Three codeanalyzer-v4 latent findings remain in production HEAD  *(nit)*
**Category:** review-gap **Missed by:** none (codeanalyzer rated; not a miss)
**Files:**
- `PLang/app/type/this.cs:69` (`IsNull => Value == "null"` string-magic vs `ReferenceEquals(this, Null)`)
- `PLang/app/data/this.cs:444` (`As(string typeName)` lost `?? GetPrimitiveOrMime`)
- `PLang/app/type/this.cs:137` (`Scheme` getter lost `?.` null-safety)

Coder v3 changed only tests + `.pr` + one goal file — none of these are
addressed. Codeanalyzer v4 said "F1 is the one worth doing — one line, kills a
literal-name footgun." It's still there. None of the three is a bug today
(`new type("null")` doesn't exist; `As(string)` has no production caller;
`Scheme` readers run on stamped entities), so they stay latent — but they're
the cleanest one-line cleanups available. Worth bundling into a follow-up if
the coder gets another round.

**Suggestion:** if/when coder is invoked again, fix F1 (`ReferenceEquals`) in
one line; document F3 as "stamped Data required" or restore the fallback;
either NRE-safe F4 or convert to a `Promote()`-style explicit throw.

### F3 — Producer-stamping invariant is correct but subtle  *(minor — cross-file)*
**Category:** cross-file **Missed by:** none (codeanalyzer noted producer stamping
is the right level, but didn't trace the propagation mechanism end-to-end).
**Files:** `PLang/app/data/this.cs:80-81` (Context setter), all `FromName(...)`
call sites (~30+).

The non-null `Data.Type` invariant depends on this chain: producer calls
`Data.@this.Ok(value, FromName("list"))`, then `Engine` (or any other consumer)
sets `data.Context = ctx` — the Context setter at `data/this.cs:80-81`
propagates onto `_type.Context`. Without that propagation, `Promote()` throws.

I verified the mechanism works (line 80: `if (_type != null) _type.Context =
value;`) and it covers all the `FromName(...)` producers I sampled
(module/list/*, module/file/read, module/crypto/code, module/signing).
**Reads on the type's fold properties (`Fields`/`Values`) before `Data.Context`
is stamped will throw.** This is the intended fail-loud behavior, but it makes
the invariant "every Data must have Context stamped before any consumer reads
`Data.Type.Fields`" load-bearing across module boundaries.

No bug observed — every code path I checked stamps Context before fold reads
happen. Recording it because it's the kind of contract that's easy to violate
in a future module without realizing it. Tester's `TypeFoldRead_OnUnstampedDomainEntity_ThrowsHard`
pins the throw direction, which is exactly the right defense.

**Suggestion:** none required — defense-in-depth is already in place via the
Promote throw. Noting for the docs bot to capture as a "Producer Stamping
Invariant" in `good_to_know.md` so future module authors know.

---

## Foundation ripple check

Files in the foundation (Data.cs, Wire.cs, type/this.cs, type/list/this.cs)
changed substantially. Spot-checked:
- `Data.Type` getter/setter — consistent (null-sentinel emit, context
  propagation, copy semantics).
- `Wire` — `typeRef` deserialization unchanged in shape; sign-if-missing path
  intact.
- `type.@this` constructors — 1-arg (no fold-loaded), 2-arg (fold-loaded for
  primitives + catalog entries). Used correctly at all call sites I spot-checked.
- `type.list.@this` BuildTypeEntries — uses 2-arg ctor everywhere
  (`list/this.cs:565,616,630`), keeping cache build under `Promote()`'s
  short-circuit.

No foundation ripple I can flag as wrong.

---

## What I did not re-do

- Per-file OBP review (codeanalyzer v1–v4 covered).
- Test-honesty mutation pairs (tester v1–v3 confirmed).
- Stage 1–3 rename audit (sealed in earlier reviews).
- A second pass of the producer-stamping in every action handler (sampled, not exhaustive).
