# tester — lazy-deserialize — summary

**Version:** v3 (matches coder v3 / codeanalyzer v2-of-this-work)

## What this is
Test-quality validation of the `lazy-deserialize` branch. The branch makes `Data` lazy
(`{raw, type, kind, value}`, value computed on first touch via a per-(type,kind) reader
registry); one boundary stamps `{type,kind}`, scalar `%x%` stays raw, navigation/`as`
materialize. v3 also fixed internal `Data→JSON` round-trips (deep-clone, wire-shape,
goal-call params) that dropped Signatures and mislabelled types.

## What was done (v3)
- Clean rebuild (stale-binary trap), ran both suites: **C# 4021/4021**, **PLang 273/273**,
  deterministic across two runs, `git status` clean after (no warm-cache `.pr` rewrite).
- Builder-false-green check on all 11 LazyDeserialize `.pr` files — every step's `text`
  matches `actions[0]`. No drift.
- Audited the 9 C# "strict" anchors that the goal smoke-tests defer to — all exist with
  strong assertions (error keys, exact values, MaterializeCount==0). Deferral is honest.
- **False-green hunt** on the headline fix `ead0caa83`: it changed `variable.set` AND
  `list.add` to `ShallowClone`, but only `list.add` got a nested-signed-Data regression
  (`SignedDataSurvivesInList`). Probed the untested `variable.set` List arm with a
  throwaway C# test → behavior is **correct** (signature survives). Gap is
  regression-pinning, not a live bug.

**Verdict: PASS.** 3 minor findings (1 missing-coverage probe-confirmed-benign, 1
weak-but-honestly-deferred assertion, 1 process: no baseline-tests.md). No critical/major.

## Code example — the gap and the probe
The asymmetry: `list.add` is pinned through the runtime —
```
- sign "hello world", write to %signed%
- add %signed% to %list%
- verify %list[0]%, write to %ok%     # green — signature survived the list
```
…but the twin `variable.set` List arm (`set %bundle% = [%signed%]`) is not. Probe
confirmed `ShallowClone` shares `_value` by reference, so the nested signed `Data`
keeps its `Signature` and verifies. Recommend adding the symmetric goal test.

## Files (tester output only — no source committed)
- `.bot/lazy-deserialize/tester/v3/{plan,result,verdict}.md|json`
- `.bot/lazy-deserialize/test-report.json`
- `.bot/lazy-deserialize/report.json` (session entry)
