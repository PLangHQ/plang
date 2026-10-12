# app-systems — plang reds, round 3 (after the builder's a5d9cce68)

Run: fresh PlangConsole at 8fe4ca9f2 (keep.Anchor in), `cd test && plang --test={"exclude":["llm","network"],…}`
→ **220 pass, 28 fail, 21 stale, 8 skipped** (the skip tags work: signing ×2, duration drop out).

## Q1. runtime — `!type.kind` reaches plang as a C# carrier (for the architect; locked `type` surface)
`%x!type.kind%` answers `app.type.clr.@this` wrapping the kind's C# string (probed: `n!type.kind` → clr "long",
`cfg!type.kind` → clr "json", `s!type.kind` → clr ""). C# reads the right names, but in plang `equals "json"` compares a
text against a carrier and fails, and a type with no kind reads `""` — never null. One cause for:
ReadConfigJson_UntouchedIsJsonString, ReadCsv_LandsAsTable, kind/strict SetAsImageStrictNoKind, SetAsTextUppercase,
SetAsTextWithMdExtension (`is null`). A *clr leak*: the kind should cross as a plang value (its name as text, null
when the type has none). `type`'s members are the locked core — a surface change for Ingi.

## Q2. question — `set %x% = 5` → kind `int` or `long`?
SetIntLiteralIsNumberInt pins "the architect's `set %x% = 5 → {number, int}` rule"; today the kind is `long` (the
storage size; `is int` answers by fit, 083a63991). Stale (rewrite to `%x% is int`) or runtime?

## R. builder
- Not rebuilt since their rewrites (`.pr` from 16:48): grantastorespersisted/ProbeAnswerer (still `%!data%`),
  module/test/tag (no `tag` stamped — built before a1a8ca80d).
- Built a fixture meant to stay unbuilt: module/test/discover/_stale/.build/unbuilt.fixture.pr → TestDiscoverReportsStaleWhenPrMissing.
- Wrong saves still in place: Cut2 + Narrow (`is file` → equals), DataTypeReadsEntity + Plane_DataKey (kind dropped),
  BareLiteralJudgedByForm (untyped literals), ListIsItemSortsAsBefore (`order:"%nums%"`), compare ×3 (`on error`
  dropped), ActorSwitch + goalchannelrecursion (actor/channel dropped), ForeachDictionary (`convert %json% from json`
  → a plain `variable.set`, so %dict% is text and the loop runs once).
- Built from the repo root: DoublePlusDecimal (carried anyway), SigningHeaderMismatch.

## S. stale — rewritten (15903dd25), builder to rebuild
module/test/report ×3: `%report.content%`/`.summaryX`/`.variableSnapshotCount`/`.reportPath` → `!` (572 — they are
the answer's properties, not members of its list of tests).

## T. still to trace
- app/callstack/Audit: now 6, the comment counts 7 (4 inner + the fourth + 2 propagation frames); ThrowItem is built.
- module/test/assert TestAssertFailureSnapshotsVariables: `list.any … where Error.Variables.foo equals 42` →
  FieldNotFound — does a where key navigate a dotted path?
- module/test/report MasksSensitiveVariablesJunit (already `!`): one `contains` fails on the junit content.
