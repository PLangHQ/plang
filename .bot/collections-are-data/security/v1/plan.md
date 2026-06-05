# Security v1 — collections-are-data

**Scope:** 49 commits since type-kind-strict merge base (`e847bf8ff`). Branch reshapes list/dict into native Data containers; chunk/row list model; value-owned comparison; new `where` action; `@schema:"data"` wire marker for Data self-identification; CopyStructure list-aliasing fix.

**Prior bots:** codeanalyzer v4 PASS (F1 list-aliasing fixed; F2 signing tests deferred as merge gate; F3 O(n²) fixed; F4 nits fixed). Tester v7 PASS (271 pass + 2 honestly Skipped, 4089/0 C#). **Merge gate:** F2 — `verify` of signed value through list[0] / goal call is REGRESSED on this branch (tester reproduced [Fail] after un-gutting goals). Documented for follow-up branch `signature-as-schema-wrapper`.

## My job

Independent security review, not a repeat of codeanalyzer. Threat-model questions specific to the diff:

1. **F2 signing regression — security severity reframe.** The deferral is honest, but I need to rate the actual hole. If a signed Data dropped into a list rehashes wrong and `verify` returns false on a structurally valid signature, that's a denial-of-trust against legitimate users (not RCE), but the *fix path* (a "schema wrapper") sits next to the signing pipeline — easy to get wrong. What is the actual reachability today, and does the disabled-test posture invite reintroducing the same bug?
2. **`@schema:"data"` wire marker.** Self-identifying wire shape sounds clean but a recognizer that promotes raw JSON into Data on the inbound path is a deserialization gadget. What decides "this is a Data envelope" vs. "this is a payload that happens to have a `@schema` key"? Can a user attacker-controlled payload force-promote to Data with attacker-chosen Type?
3. **chunk/row list model + CopyStructure.** F1 was list aliasing — the fix structurally copies on insert. (a) Does CopyStructure recurse without depth guard (recursive lists DoS)? (b) Does the "shared by reference for leaves/dicts" exception leak any mutable shared state I should flag?
4. **`where` action.** New filter primitive. Where does the filter expression come from? If it's an action parameter that templates user values, can untrusted data write the filter and exfiltrate via `where` over a sensitive list?
5. **Dict as native object type.** New `dict/this.cs` — how does it serialize, does Json reader cap depth / size, does navigator give path-traversal-shape access into nested dicts?
6. **Honest-Skipped tests as security pattern.** Skip-tag tests still count toward 273/273 only via "skipped" arm — confirm tester's HasSkipTag check doesn't silently swallow other tests via tag collision.

## Approach

1. semgrep scan first (baseline 15 INFO; surface deltas).
2. Read the F2 disabled goals, the integration tests, and the `Wire`/`Sign`/`Verify` path that rehashes — figure out the actual breakage mode.
3. Read `Wire.cs`/`LiftDataIfShaped` and the `@schema:"data"` recognizer.
4. Read `list/this.cs` (CopyStructure, chunk/row), `dict/this.cs`, `where.cs`.
5. Rate by PLang threat model — wire/external-data path = real; in-process aliasing = correctness, only security if it crosses the signing or sensitive boundary.
6. Verdict + report + push.
