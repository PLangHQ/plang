# Security summary — `data-serialize-cleanup`

**Version:** v2 (latest)
**Date:** 2026-05-28
**Verdict:** PASS

## What this is

PLang's wire-serialization cleanup branch — ISerializer tightened to `Data`, application/plang serializers merged, sign-if-missing moved into the wire converter, hash bytes canonicalized through the same outbound options, Compress flattened, Properties get a nested wire scope + `!` operator.

Codeanalyzer v2 PASSED. Tester v2 PASSED with mutation verification.

## v2 vs v1

**v1 raised F1 (HIGH): pre-auth StackOverflow DoS via `WireJsonConverter.LiftDataIfShaped` recursion. Verdict was FAIL.**

**v2: F1 retracted to Info as a false-positive.** Mutation test (set `MaxReadDepth = int.MaxValue` in the post-fix code) showed the 200-level depth-bomb regression test **still passed**. That means the new AsyncLocal counter is not the gate — STJ's per-reader `MaxDepth=64` is.

The reasoning I missed in v1: `JsonDocument.ParseValue(ref reader)` consumes from the **same** reader and inherits its `MaxDepth`. A 200-deep payload triggers `JsonException` at depth 64 inside the **outermost** `ParseValue` call. No `LiftDataIfShaped` recursion happens. Each recursive `Deserialize<@this>(rawText)` is fed a sub-tree at most `MaxDepth-1` deep, so the C# ladder shortens by one level per step rather than growing unboundedly.

The fix coder landed (AsyncLocal counter + 3 regression tests) is harmless and worth keeping as defense-in-depth — it covers the case where a future caller raises `options.MaxDepth` to allow deeper non-Data graphs. But it doesn't close a real exploitable vulnerability.

## Findings (final state)

| ID | Severity | Status | Area |
|----|----------|--------|------|
| F1 | Info (was HIGH) | fixed (defense-in-depth) | WireJsonConverter recursion depth — STJ MaxDepth already gates |
| F2 | Low | open | application/plang serializer omits Sensitive filter (intentional for settings; widened by Stage 2) |
| F3 | Low | open | Properties.EnsureSupportedValue top-level only; List<Data>/Dict<Data> round-trip asymmetrically |
| F4 | Info | open | sign-if-missing silently no-ops when Context.Actor==null |

Full machine-readable form: `.bot/data-serialize-cleanup/security-report.json`.

## What I'd flag for the user

I called HIGH and FAIL on v1 based on an incomplete trace. The fix landed cleanly and is defensible, but the severity was wrong. The honest record is here for future bots so the lesson sticks (also captured in `feedback_pre_auth_parse_severity.md` memory): pre-auth + unrecoverable + reachable today is a real rating signal, BUT verify the stack-blow path is actually unbounded — `Utf8JsonReader.MaxDepth` self-enforces across `ParseValue` and inherited through to extracted sub-trees, so a "depth-reset recursion" claim needs the source-reader MaxDepth check to actually fail before the claim holds.

## Next bot

**Verdict: PASS**

```
run.ps1 auditor data-serialize-cleanup "Review the code on branch data-serialize-cleanup" -b data-serialize-cleanup
```

Next bot: auditor
