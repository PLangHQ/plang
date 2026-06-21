# codeanalyzer v1 — report — `template-stamping-at-read`

## Scope & coverage (read this first)

This is the **first** codeanalyzer review on this branch, and the branch is
enormous: it carries the **entire `scalars-as-native` refactor** (527 commits
off `runtime2`; 361 production C# files; +10359/-5157) on top of the recent
template-stamping work the coder summarized as B1–B5.

A full line-by-line pass over 361 files in one session would be theatre, not
review. This report is **risk-prioritized**. What I reviewed deeply, what I
sampled, and what I deferred is stated explicitly so the verdict isn't mistaken
for blanket certification.

**Reviewed deeply:** handoff B1–B5 (incl. security-critical Ed25519 signing);
the `item.@this` apex; the comparison collapse (`Comparison` enum +
`app.type.compare` mediator); the condition `Operator`; the two `fix(...)`
commits; and the mandated mechanical passes across the whole changed production
set.

**Sampled / relied on prior rounds:** the ~120-file `app/type/**` scalars body,
carried by the architect → test-designer → coder review rounds in the branch
history.

**Build state:** `dotnet build PlangConsole` → **0 errors** (clean). PLNG002
(System.IO gate) and PLNG001 (property-kind gate) both pass at error severity,
so they are green by construction.

---

## Mechanical passes (whole changed production set)

| Pass | Result |
|---|---|
| **System.IO ban** | **Clean.** One hit — `this.Snapshot.cs:107` throws `System.IO.FileNotFoundException` — is an *exception type*, not a filesystem reach. The real load one line up goes through the gated `path.@this.Resolve(...).LoadAssemblyAsync()`; the throw is caught & rewrapped two lines down. Bypasses nothing. (Confirmed for Ingi mid-review.) |
| **Console.* ban** | **Clean.** All three hits are in comments / an example string (`type/code/this.cs`), no actual writes. |
| **OBP Rule 9 — courier `.Value` (is\|as\|switch)** | **Clean.** Every hit is `KeyValuePair.Value` (variable-store dicts, error kvps), a comment, or a leaf serializer reading its own typed options — none is a relay opening `Data.Value` mid-flight. |
| **Provenance / history comments** | **Clean on this branch's authored lines.** One pre-existing inherited comment noted below (N3). |

---

## Findings

### F1 — Dead method `BothPresent` in `condition/Operator.cs:87` (deletion test) — LOW
```csharp
private static bool BothPresent(data.@this? left, data.@this? right)
    => left?.HasValue == true && right?.HasValue == true;
```
- **Zero callers.** `grep BothPresent` across `PLang/` + `PLang.Tests/` returns
  only this definition (the one test hit, `SnapshotEntry_..._BothPresent_Distinct`,
  is an unrelated test-method name). Delete lines 86–88 and nothing breaks — the
  deletion test fails it.
- **Its premise is also stale.** The doc says *"the ordering operators are false
  otherwise"* — but the ordering boundary (`Ordered`, line 48) now **throws
  `IncomparableException`** on a missing operand; it does not return false. So the
  method documents behavior the file no longer has.
- **Trivially fixable** — leave for coder: delete the method.

### F2 — `Comparison.cs` — enum doc orphaned onto the exception; enum itself undocumented — LOW
`PLang/app/data/Comparison.cs:3–20`
- Two `/// <summary>` blocks stack back-to-back (lines 3–12 and 13–19), both
  immediately preceding `public sealed class IncomparableException`. In C# both
  attach to that one declaration — so the exception gets a **double `<summary>`**
  (malformed doc), while the *`Comparison` enum* (line 22), which the first block
  is clearly written for ("The single, sign-free result of every comparison…"),
  ends up with **no doc comment at all**.
- **Fix:** move the first summary block down to sit directly above
  `public enum Comparison`. Trivially fixable — leave for coder.

### F3 — `item.@this` has two `Write` methods with different roles — LOW (readability)
`PLang/app/type/item/this.cs:70` and `:397`
```csharp
public virtual bool Write(string key, object? value)         // set a child slot (mutate)
public virtual void Write(IWriter writer)                    // serialize bare wire form
```
- Two semantically unrelated operations (write-a-child vs serialize-to-wire) share
  the name `Write`, distinguished only by argument type. The doc on the first even
  has to say *"Distinct from `Write(IWriter)`"* — a tell that the name is doing
  double duty. A reader seeing `value.Write(...)` must check the argument to know
  whether it mutates or serializes.
- **Suggest** renaming one — e.g. the child-set to `SetChild(key, value)` (it
  already returns a "handled?" bool, which reads naturally), or the serializer to
  `WriteTo(writer)`. Not a blocker; flag for a future naming touch since it's on
  the apex every value inherits.

### F4 — `Normalize` silently mangles an unrecognized raw-CLR leaf — MEDIUM, **systemic / pre-existing**
- Documented by the commit that worked around it (`c27c37c5a`, fix(llm) cache):
  *"A JsonElement does not survive the cache's disk serialization — Normalize has
  no JsonElement leaf arm, so it reflects to its ValueKind property
  (`{"valuekind":"Object"}`), losing all content."* That silent corruption took
  down **every cached LLM build** until the cache stopped storing a `JsonElement`.
- The cache commit is a correct **root-cause** fix *for the cache* (store the
  unwrapped native value + the lossless `RawResponse` string, re-parse via a shared
  `ParseResultValue` helper). **But the underlying foot-gun remains:** `Normalize`
  answers an un-narrowed/unknown raw CLR leaf by reflecting it to a property bag
  instead of failing loudly. The *next* raw type that slips past born-native
  construction corrupts the same silent way.
- **Recommendation (hardening, not a branch blocker):** `Normalize` should throw a
  loud, named error on an unrecognized non-item leaf rather than reflect it. This
  is **systemic** (not local to this diff) — belongs on `obp-cleanup.md` or a
  hardening todo; flagging per character pass-4, leaving the write to architect/docs.

### F5 — B5 bracket-resolution mirrors only the top call frame, not the full cascade — LOW
`PLang/app/variable/list/this.cs:868` (`ResolveVariablesInPath`)
- The latent un-awaited-async bug is **genuinely fixed** (sync `Peek` lookup
  instead of interpolating a `ValueTask`) — good catch and a clean fix.
- Minor behavioral note: the sync probe checks only `Calls.Current` (the top
  overlay frame) → `_variables` store. `Get`'s real precedence cascades
  overlay → caller chain → store. So `addresses[idx]` where `idx` lives in a
  *parent* call frame (not Current, not root) won't resolve and the bracket stays
  literal. Low risk for index variables (typically local), but the handoff's claim
  "mirroring Get's precedence" is only true for the top frame. Acceptable as-is;
  the coder's choice to inline rather than extract a shared sync primitive is the
  right call until a third sync-probe site appears.

---

## Notes (reviewed, no action)

- **N1 — Ed25519 sign/verify clock fallback asymmetry (B3, security):** `SignAsync`
  reads `NowUtc` with `Clr<DateTimeOffset>(default)`; `VerifyAsync` falls back to
  `Clr<DateTimeOffset>(DateTimeOffset.UtcNow)`. The asymmetry is **intended and
  documented**: sign always runs mid-step (NowUtc present; the `default` is pure
  NRE-avoidance), verify can run at the deserialize boundary (no step → wall clock
  is the honest freshness reference). Sound. The only theoretical wart: *if* NowUtc
  were ever missing during sign, `Created`/`Expires` would stamp year-0001 — but
  that path doesn't occur. No action.
- **N2 — `app.type.compare` mediator (`compare/this.cs`):** reflection-discovered
  static per-family hooks (`CompareRank`, `Compare(object,object)`), cached, no
  App in scope — mirrors the established `app.type.convert` pattern. No type-switch
  in the registry; behavior lives on each family class. OBP-clean.
- **N3 — pre-existing provenance comment:** `module/variable/compress.cs:11` carries
  *"Stage-3 of data-serialize-cleanup: …"* (a dead branch name in a doc comment).
  **Inherited**, not authored on this branch, and in the class doc not the changed
  body — left for a docs sweep, not raised as a new finding.
- **N4 — Fluid fix (`d2d2f557a`):** boundary `ValueConverter` with zero-copy
  read-through views (`NativeDictView`/`NativeListView`); the ToRaw deep-copy draft
  was explicitly rejected. Correct anti-corruption adapter at the third-party
  template boundary — not an OBP smell.
- **N5 — `item.@this` apex:** dense (≈18 virtual members) but every member is
  behavior a leaf overrides, not a type-switch in a registry — this is exactly the
  OBP "behavior on the element" shape. Storage-free (smell #6 honored: no value
  blob on the base). Reviewed OBP-clean.

---

## Verdict

**PASS** (on the risk-prioritized surface reviewed).

The high-risk surface — security-critical signing reads, the `item` apex, the
comparison collapse, the condition operator, and the two fix commits — is sound.
All four mandated mechanical passes are clean and the build is green. The
findings are all **LOW** except F4, which is a **pre-existing systemic** hardening
item, not introduced or worsened by this branch.

**Non-blocking cleanups for the coder to optionally fold before merge:** delete
dead `BothPresent` (F1); move the orphaned enum summary in `Comparison.cs` (F2).
Both are one-line touches.

**This is not a certification of the full 361-file scalars-as-native body** — that
breadth rests on the prior architect/test-designer/coder rounds and the green C#
suites. If exhaustive coverage of the scalars body is wanted, it warrants its own
dedicated review pass (and is a natural multi-agent/workflow candidate given the
file count).
