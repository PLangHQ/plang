# codeanalyzer — lazy-deserialize — summary

**Version:** v2 (re-review of coder's response to v1 findings)

## What this is
`lazy-deserialize` makes `Data` lazy: `Data { raw, type, kind, value }` where `value` is
computed once, on first touch, from the raw source form via a per-(type,kind) **reader
registry** (read-side mirror of the renderer). One boundary (`channel.read`) stamps
`{type,kind}`; `file.read`/`http.get` stop deserializing. Scalar `%x%` returns the raw form
unparsed; navigation `%x.field%` and `as <type>` materialize. The v3 work also fixed internal
`Data→JSON` round-trips (deep-clone / wire-shape / goal-call param) that dropped Signature and
mislabelled types.

## v1 → v2 (review round)
v1 verdict was **NEEDS WORK** on blocker F1 + F2. Coder pushed `55037aa32` addressing the
findings. v2 re-review verdict: **PASS**.

### v1 findings, final disposition
- **F1 (Medium, blocker) — RESOLVED.** Coder documented the collection-element contract on
  `WrapItem` (element is a `Data` or a bare value; reads normalize) and added the regression
  test I called for: `SignedDataSurvivesInList.test.goal` (`sign → add to %list% → verify
  %list[0]%`), **green, 28ms, deterministic**. The deeper storage unification is correctly a
  design call routed to architect (collections-are-data).
- **F3 (Low) — RESOLVED.** Duplicated static-`Resolve` block extracted to one
  `TryStaticResolve<T>` helper; verified behavior-preserving.
- **F5 (Low) — RESOLVED.** Comment naming why three strict-kind checks exist (build / run /
  load-seam).
- **R1 — RESOLVED.** Flaky live-httpbin goal test disabled in-goal like its 8 siblings; C#
  `HttpChannelTests` covers the probe deterministically.
- **F4 (Low) — open, as filed (flag-don't-block).**
- **F2 (Low/Med) — OPEN, deferral UNTRACKED.** The `Materialize()`/`Materialise()` one-vowel
  naming footgun is unchanged. Commit claims it was "routed to the collections-are-data
  architect handoff" with the ctor's `UnwrapJsonElement` decompose — defensible rationale, but
  **no handoff/todo artifact exists**. Required follow-up: rename the seam, or actually file
  the handoff so it isn't lost. Not a blocker.

## Deterministic baseline (v2)
Rebuilt (`this.cs` changed → stale binary). **Goal 273/273, C# 4021/0.** No regression.

## What to do next
F1 is resolved with the regression test; suites green. **PASS.** Next bot: **tester**.
Carry forward the single open item: F2 (rename or file the collections-are-data handoff).
