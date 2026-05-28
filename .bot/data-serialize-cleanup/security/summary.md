# Security summary — `data-serialize-cleanup`

**Version:** v1
**Date:** 2026-05-28
**Verdict:** PASS (1 Medium open, 3 Low/Info open — none block merge per critical/high rule)

## What this is

PLang's wire-serialization cleanup branch: ISerializer input/return both tightened to `Data`, application/plang serializers merged into one canonical home, sign-if-missing moved into the wire converter walk, hash bytes canonicalized through the same outbound options so the outer signature transitively binds inner Datas. Stage 3 flattens Compress to `{type=archived, value=byte[]}`. Stage 4 adds a nested `properties` wire scope and the `%x!key%` operator. Stage 5 drops "envelope" vocabulary.

Codeanalyzer v2 PASSED (11 v1 findings closed, 2 tradeoff-documented). Tester v2 PASSED with mutation verification. My pass focused on what structural review and behavioural tests don't catch: depth bombs, sign-skip carveouts, sensitive-leak surfaces, and round-trip asymmetries.

## What was done

Reviewed the four security-load-bearing surfaces:

- **Wire converter** (`PLang/app/data/WireJsonConverter.cs`) — sign-if-missing logic, MarkOuterForHash ref-counting, the LiftDataIfShaped Data-rehydration path.
- **Canonicalization** (`PLang/app/modules/crypto/code/Default.cs`) — Hash routes through the registered application/plang serializer, type-mismatch returns SerializerMismatch 500.
- **`!` operator parser** (`PLang/app/variables/Variable.cs`) — malformed shapes flagged IsMalformed, fail-closed in variable.set with InvalidVariableReference 400.
- **Properties insertion gate** (`PLang/app/data/Properties.cs`) — EnsureSupportedValue rejects Data instances (the load-bearing Stage 4 invariant).
- **Decompression** (`PLang/app/data/this.Transport.cs`) — 100 MB output cap.
- **Sensitive-filter wiring** (`PLang/app/channels/serializers/serializer/plang/this.cs`) — plang intentionally omits Strip; Json applies it.
- **Semgrep baseline** (`scripts/semgrep-scan.sh`) — 14 findings (down from 15 baseline; all serializer-default-options informational).

## Findings

| ID | Severity | Area | Status |
|----|----------|------|--------|
| F1 | Medium | WireJsonConverter recursion depth reset via GetRawText round-trip → StackOverflow DoS | open |
| F2 | Low | application/plang merged serializer omits Sensitive filter (intentional for settings persistence, widened scope by Stage 2) | open |
| F3 | Low | Properties.EnsureSupportedValue top-level only; List<Data>/Dict<Data> round-trip asymmetrically (signed-then-flattened) | open |
| F4 | Info | sign-if-missing silently no-ops when Context.Actor==null; depends on receive-side signing.verify as primary gate | open |

Full machine-readable form: `.bot/data-serialize-cleanup/security-report.json`.

### F1 — the one I'd land before merge

`WireJsonConverter.Read` consumes a JSON value via:

```csharp
using var doc = JsonDocument.ParseValue(ref reader);
value = LiftDataIfShaped(doc.RootElement, options);
// → JsonSerializer.Deserialize<@this>(element.GetRawText(), options);
```

`GetRawText()` + `Deserialize<@this>(string, options)` creates a **fresh** Utf8JsonReader with depth=0. STJ's `MaxDepth=64` cap applies *per* parse — not cumulatively across the recursive Data levels. A ~500-level nested payload of `{"name":"a","value":{"name":"a","value":{...}}}` (~11 KB on the wire) creates ~500 × ~5 C# stack frames → StackOverflowException → unrecoverable process crash.

Five-line fix: thread an AsyncLocal counter through the converter, throw `JsonException` past 64 nesting levels. Regression test: 200-level nested input must `JsonException` not `StackOverflowException`.

## Next bot

**Verdict: PASS**

If you want F1 closed before merging the branch back to runtime2, re-spin coder:

```
run.ps1 coder data-serialize-cleanup "Fix F1 (security v1): WireJsonConverter recursion depth reset via GetRawText round-trip → StackOverflow DoS. See .bot/data-serialize-cleanup/security/v1/plan.md" -b data-serialize-cleanup
```

If you accept the Medium as a same-area follow-up and proceed:

```
run.ps1 auditor data-serialize-cleanup "Review the code on branch data-serialize-cleanup" -b data-serialize-cleanup
```

Next bot: auditor (or coder if F1 closes here)
