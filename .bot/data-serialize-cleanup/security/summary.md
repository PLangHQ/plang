# Security summary — `data-serialize-cleanup`

**Version:** v1
**Date:** 2026-05-28
**Verdict:** FAIL — F1 is a pre-auth unrecoverable DoS, rated High

## What this is

PLang's wire-serialization cleanup branch: ISerializer input/return both tightened to `Data`, application/plang serializers merged into one canonical home, sign-if-missing moved into the wire converter walk, hash bytes canonicalized through the same outbound options so the outer signature transitively binds inner Datas. Stage 3 flattens Compress to `{type=archived, value=byte[]}`. Stage 4 adds a nested `properties` wire scope and the `%x!key%` operator. Stage 5 drops "envelope" vocabulary.

Codeanalyzer v2 PASSED. Tester v2 PASSED with mutation verification. My pass focused on what structural review and behavioural tests don't catch.

## What was done

Reviewed the security-load-bearing surfaces (wire converter, canonicalization, `!` parser, Properties gate, decompression, Sensitive wiring) and ran the semgrep baseline (14 findings, no new violations).

## Findings

| ID | Severity | Area | Status |
|----|----------|------|--------|
| F1 | **High** | WireJsonConverter recursion depth reset via GetRawText round-trip → pre-auth StackOverflow DoS | **open (blocks merge)** |
| F2 | Low | application/plang merged serializer omits Sensitive filter (intentional for settings persistence, widened scope by Stage 2) | open |
| F3 | Low | Properties.EnsureSupportedValue top-level only; List<Data>/Dict<Data> round-trip asymmetrically | open |
| F4 | Info | sign-if-missing silently no-ops when Context.Actor==null | open |

Full machine-readable form: `.bot/data-serialize-cleanup/security-report.json`.

### F1 — blocks merge

`WireJsonConverter.Read` consumes a JSON value via:

```csharp
using var doc = JsonDocument.ParseValue(ref reader);
value = LiftDataIfShaped(doc.RootElement, options);
// → JsonSerializer.Deserialize<@this>(element.GetRawText(), options);
```

`GetRawText()` + `Deserialize<@this>(string, options)` creates a **fresh** `Utf8JsonReader` with depth=0. STJ's `MaxDepth=64` applies *per parse* — each recursive Data level restarts the budget. ~500-level nested `{"name":"a","value":{...}}` (~11 KB) → ~500 × ~5 C# stack frames → `StackOverflowException` → unrecoverable process crash.

**Why High, not Medium:**
- **Pre-auth.** Parsing happens before `signing.verify` runs. Attacker does not need a verified identity.
- **Unrecoverable.** .NET treats StackOverflow as fatal; no catch unwinds.
- **Already reachable.** `callback.run` deserializes wire bytes into Data today (standing memory finding). The deserializer is exposed to untrusted bytes through that path — not contingent on a future external channel.
- **Cheap.** ~11 KB payload, trivially repeatable from outside the trust boundary.

**Fix:** thread an AsyncLocal recursion counter through `WireJsonConverter.Read`, throw `JsonException` past 64 nesting levels. Regression test: 200-level nested input must `JsonException`, not `StackOverflowException`.

## Next bot

**Verdict: FAIL**

Issues: F1 — pre-auth StackOverflow DoS in `WireJsonConverter.LiftDataIfShaped`.

```
run.ps1 coder data-serialize-cleanup "Fix F1 (security v1 HIGH): WireJsonConverter.LiftDataIfShaped resets STJ MaxDepth on each recursion via GetRawText+Deserialize<@this>(string) — pre-auth StackOverflow DoS reachable through callback wire deserialization. See .bot/data-serialize-cleanup/security/v1/plan.md and .bot/data-serialize-cleanup/security-report.json F1." -b data-serialize-cleanup
```

Next bot: coder
