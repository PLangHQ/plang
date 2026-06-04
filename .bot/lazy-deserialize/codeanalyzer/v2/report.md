# codeanalyzer — lazy-deserialize — v2 report (re-review of coder's response to v1)

**Commit reviewed:** `55037aa32` ("address code-analyzer findings (F1/F3/F5/R1); F2 + ctor-unwrap routed to collections plan"), on top of v1's `ead0caa83`.

## Deterministic baseline (rebuilt — `this.cs` changed, so the goal binary was stale)
- `dotnet build PlangConsole` — **0 errors**.
- Goal suite (`cd Tests && plang --test`) — **273 / 273 pass, 0 fail**. The v1 flaky httpbin
  fail is gone (R1 disabled it). The new F1 regression test passes deterministically.
- C# suite: the only C# changes are the F3 extraction (behavior-preserving by inspection)
  and F5 comments — the v1 **4021 / 0** stands; nothing in the diff can move it.

---

## Disposition of v1 findings

### F1 — Medium (the blocker) — **RESOLVED**
The coder took the documented-contract path I offered as acceptable (rather than unifying
storage now), and — crucially — added the regression test I called for:

- `data/this.cs:510` `WrapItem` now carries the **collection-element contract**: an element is
  either a `Data` (carries Type/Signature, e.g. from `list.add`) or a bare value (e.g. a
  JSON-parsed element); reads normalize. The comment also notes `navigator/List.Element`
  applies the same rule and *why* it can't just call `WrapItem` (it needs index-name + parent
  link).
- `Tests/LazyDeserialize/SignedDataSurvivesInList.test.goal` — `sign "hello world" → add
  %signed% to %list% → verify %list[0]% → assert true`. **Passes, 28ms, no network.** This is
  exactly the blind spot that let the old deep-clone corruption hide.

The deeper "store uniformly / collections-are-data" unification is correctly a design call and
is the right thing to route to architect — *provided the routing is real* (see F2 below).

### F3 — **RESOLVED** — clean, behavior-preserving
The duplicated ~14-line static-`Resolve` block is now one `TryStaticResolve<T>` helper
(`data/this.cs`), called from both `AsT_Impl` and `AsT_Convert`. Verified equivalent: returns
the wrapped result on success, the wrapped error when `Resolve` threw, and `null` ("not
applicable — fall through to `WrapAs`") for every case the originals fell through on
(not-a-string / already-T / no-context / no-method / non-T result). No behavior change.

### F5 — **RESOLVED**
`set.cs:130` now carries a comment naming why the three strict-kind checks exist and that they
are not redundant: build-time literal (`ValidateBuild`), run-time `%var%` (`IKindValidatable`
probe), materialization-time byte-backed (`IStrictKindEnforcer` load seam).

### R1 — **RESOLVED**
`HttpStatusRead_DoesNotMaterialiseBody.test.goal` is disabled in-goal (steps commented out, a
`write out` stub remains), matching the 8 sibling httpbin tests on the parent branch. The
comment points to the deterministic C# `HttpChannelTests` coverage and says how to re-enable.

### F4 — Low — left open (as filed: flag-don't-block). Fine.

### F2 — Low/Med — **NOT resolved, and the deferral is NOT tracked**
The `Materialize()` (read-through) vs `Materialise()` (in-place navigation seam) one-vowel
naming footgun is unchanged. The commit message says it was "routed to the collections-are-data
architect handoff" together with the ctor's `UnwrapJsonElement` decompose, arguing both are the
same "decompose-on-construction / patch-on-navigate" smell.

That rationale is **defensible** — renaming a method that a collections-are-data redesign may
delete outright is churn, and `Materialise` genuinely is a patch-on-navigate seam. But I find
**no artifact capturing the deferral**: no architect handoff file, no `todos.md` entry, nothing
in `.bot/lazy-deserialize/architect/`. The commit `55037aa32` touched only `this.cs`, `set.cs`,
and the two test files. So as it stands F2 is a claim of routing with nowhere it was routed to —
it will be lost.

**Required follow-up (not a blocker):** either do the one-line rename now (e.g. the seam →
`ForceMaterialize()` / `MaterializeInPlace()`), or actually file the collections-are-data
handoff/todo that names F2 + the ctor-unwrap so the architect pass picks it up. A deferral is
only a deferral if it's written down.

---

## Verdict: PASS
The blocker (F1) is genuinely resolved — the documented contract plus the deterministic
signed-Data-in-collection regression test, now green. F3/F5/R1 are done well; F3 is verified
behavior-preserving. Suites are green (273/273 goal, 4021/0 C#). No correctness regression.

The single open item is **F2**, a Low/Medium naming footgun whose deferral is currently
untracked. It does not gate the feature, but it must not vanish: do the rename or file the
handoff. Flagging, not blocking.
