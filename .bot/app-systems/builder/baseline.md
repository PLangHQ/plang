# app-systems — reviewer-bot baseline (round 5)

**Suite:** `cd test && ../PlangConsole/bin/Debug/net10.0/plang --test`, against HEAD (incl. coder's Freeze/Cover refactor b982cbc94).

**Totals: 276 — 245 pass / 16 fail / 12 stale / 3 skipped.** (Progression: 220/33/21 → 231/30/12 → 239/22/12 → **245/16/12/3**.)

No builder-mapping refusal that a teaching edit can fix remains. Every fail/stale below is a coder-domain runtime red, a carried builder limitation, a next-branch item, or by-design-unbuilt.

## Fails (16)

| test | owner | reason |
|---|---|---|
| app/data/DataTypeReadsEntity | coder / next-branch | kinded-Type (kind redesign 707–712) |
| app/data/Plane_DataKeyVsProperty | next-branch | kinded Type `{text, json}` not yet a decider Type choice |
| app/data/Cut2 | coder (triage) | save fixed (json content, `.db` navigates, db/port asserts pass) + is-file build fixed; now fails on `%cfg% is dict` after navigating a json file answering false — likely a runtime bug (Operator.cs:177–183 IsType reads/narrows/answers from the chain) |
| app/data/Narrow_ChainWideBangBothBranches | coder (triage) | same as Cut2 — `%config% is dict` after the json-file narrow answers false |
| app/type/list/ListIsItemSortsAsBefore | carried-builder (A) | writer emits an empty query for a no-field sort |
| app/type/item/BareLiteralJudgedByForm | coder | kind/type runtime |
| app/type/kind/strict/SetAsImageStrictNoKind | coder / next-branch | kind redesign (also a held-5 kind test) |
| app/type/kind/strict/SetAsTextSlashMarkdownNormalises | coder / next-branch | kind redesign |
| app/type/kind/strict/SetAsTextSlashMarkdownStrictUnverifiable | coder / next-branch | kind redesign |
| app/type/kind/strict/SetAsImageGifStrictMismatch | coder / next-branch | kind redesign |
| module/condition/compare/Failure_DictEqNumber | next-branch (issue 76) | on-error clause on a block-if dropped — builder can't parse |
| module/condition/compare/Failure_DictOrderDict | next-branch (issue 76) | same (on-error on block-if) |
| module/condition/compare/Cut5 | next-branch (issue 76) | same — `if %d% > %n% then` + indented `on error call CaughtOrder` saved as condition.if alone, clause dropped |
| module/channel/goalchannelrecursion | carried (next-branch: channel slot offers) | helper EchoBack `write %message% to echo` drops the channel — the channel slot is a CHOICE over the step's variables + none, so a literal channel name (`echo`) is never offered; the decider picks none. The slot should offer the channels the goal has registered (the build simulates `set channel "echo"`) |
| module/signing/headermismatch/SigningHeaderMismatch | carried-builder (B3) | decider pre-empts the main action when an on-error clause trails |
| module/math/DoublePlusDecimal_Errors | carried-builder | `as number/decimal` kind dropped (kinded Type choice) |

## Stale (12) — unbuilt, each by reason

| test | owner | reason |
|---|---|---|
| module/error/ThrowAttachesData | next-branch (issue 74) | `%s%.length%` nav / throw-attaches-data |
| app/type/text/TextLengthOp | next-branch (issue 74) | `%s%.length%` nav mangle |
| app/type/null/MissingVarIsNotNullValue | next-branch (issue 74) | |
| app/type/null/NullBareOnJson | next-branch | null-on-json |
| module/llm/LlmProperties | next-branch | llm Limit-padding |
| module/signing/RoundTripAfterRawSignatureDeletion | next-branch (issue 75) | signing round-trip |
| module/list/query/Failure_SortMixedIncomparable | carried-builder (A) | writer emits an empty query for a no-field sort |
| plan/app-systems/app/setting/UnknownSettingIsAnError | next-branch | set an unknown `%!…setting.X%` at build |
| plan/app-systems/app/type/list/TypedListHoldsItsType | next-branch | kinded Type choice |
| plan/snapshot/module/callback/askvarsresumebindsvalue/Start | by-design | snapshot plan — `%!ask.answer%` resume not valid yet |
| plan/snapshot/module/callback/statefulaskmidgoalbindsvalue/Start | by-design | snapshot plan |
| plan/snapshot/module/callback/statelesscrossgoalresumes/Start | by-design | snapshot plan |

## Owner summary
- **coder** (runtime reds / kind-redesign C#): DataTypeReadsEntity, BareLiteral, 4 kind/strict.
- **coder (triage)**: Cut2, Narrow — save + is-file builds FIXED; `%cfg% is dict` after navigating a json file answers false (likely an IsType runtime bug, Operator.cs:177–183).
- **carried-builder** (no teaching fix this branch): ListIsItemSortsAsBefore + Failure_SortMixedIncomparable (A, empty query), SigningHeaderMismatch (B3), DoublePlusDecimal.
- **next-branch** (deferred, listed with Ingi): Plane_DataKey + TypedListHoldsItsType (kinded Type choices); Failure_DictEqNumber/DictOrderDict/Cut5 (issue 76 — on-error on a block-if); goalchannelrecursion (the channel slot should offer the goal's registered channels, not just the step's variables, so a literal channel name is offered); issue-74 trio; LlmProperties; RoundTrip + UnknownSettingIsAnError; the empty-query Parse-preempt seam; `throw` in an if-body → `throw.error` (reader rejects).
- **FIXED this pass (teaching)**: `is`/`is not <type>` is the type check (not `==`/`!=`) — ReadCsv_LandsAsTable now a real pass (was pass-by-luck), Cut2/Narrow build correctly. output.write bare `to <name>` channel (literal form). llm.query schema spelled schema/scheme/block. on.error after an erroring `if`.
- **by-design**: 3 snapshot callback Starts.
- **3 skipped**: signing pair + Duration (skip-tag).
