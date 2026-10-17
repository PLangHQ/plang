# auditor v1 — kind-is-type

**Scope:** the whole kind-is-type branch, reviewer line complete. Parent `runtime2@cf66e2d01`, tip `49c09aaf6`, 275 commits ahead. Reviewer verdicts to re-check: codeanalyzer v2 PASS (560fc0a80), v3 PASS carrying F3.v3 (58348d1cc); tester v1 FAIL → coder d03051109 → v2 PASS (356d9ca44); coder N1 follow-up (4db505aac, test-only); security v2 PASS (49c09aaf6).

**Method per the character:** no fresh file-level OBP pass — that is codeanalyzer and security work, done and read in full. I looked at the seams between the reviewers, the cross-file contracts a file-level pass can't see, and the plan-against-code gap at merge.

## Verdict: PASS

Next bot: **docs**.

Two findings, both non-blocking: one plan-vs-code gap the architect has not closed (F-SC-1 strict carry), one confirmation that the codeanalyzer's F3.v3 is the only new name-check introduced on the branch (F-FL-1, informational). The reviewer line's verdicts hold.

HASH: pending my commit  
CHECK: tester v2 (356d9ca44) stands — C# green except the 6 Wire baseline + timing (identical on runtime2); plang 294/264/15/12/3 all baseline (none new vs runtime2's 15 reds)  
OPEN: F-SC-1 (the plan Why's four kind/strict reds, still red at merge); F3.v3 carried to branch A by the codeanalyzer

## Previous reviews assessed

| reviewer | verdict | my read |
|---|---|---|
| codeanalyzer v2 | PASS | **agree** — the single `data.Open()` door, the loop-free `OwnInternal` chain, the gone navigator and CS0108-clean core all hold on direct read (details below) |
| codeanalyzer v3 | PASS, carrying F3.v3 | **agree** — F3.v3 (`Fluid.cs:165 format is { Name: "item" }`) is a real symptom-patch and the clean fix is a locked-kind addition, correctly routed to branch A, not downgraded |
| tester v1 | FAIL (F1, F2) | **agree** — both false greens were real; F1 was a materialize-before-assert gap, F2 had no deterministic kind-follow-up pin |
| tester v2 | PASS, N1 noted | **agree** — the F1/F2 pins go red under M5a/M8 (I read `KindFollowUpTests` in full); N1 (the `!k.Internal` filter unpinned) was then closed by the coder's 4db505aac (two deterministic cases in the same file) |
| security v2 | PASS | **agree** — the Utf8JsonReader default MaxDepth=64 ≥ Wire.MaxReadDepth=64 claim checks; `file.read`'s Type slot keeps `AuthGate(read)` before the declared type's birth; the render-fix is a landed-value type change, not a sandbox change |

## Cross-file seams I checked (what a per-file reviewer wouldn't catch)

1. **`data.Open()` is really the one parse-once door.** Codeanalyzer named 7 use-sites; I verified by `grep -rn "await .*\.Open()"` across `PLang/app/`: six Data.Open callers (`app/this.cs:431` Identity decode, `data/Operator.cs:182` the `is` operator, `data/this.cs:505` EnumerateItems, `data/this.cs:636` Compare, `type/item/source.cs:335` the courier `Get`, `module/llm/code/OpenAi.cs:398` the Schema-opens-answer path) plus `Value<T>:557`. No consumer re-implements parsing; no residue of the old navigator. The parse-at-use model is one door with narrow catches (`JsonException`/`FormatException`/`InvalidOperationException`/`NotSupportedException`). The 805-cluster migration is complete.

2. **No new "is X? by name" check hidden elsewhere on the branch.** `git diff origin/runtime2..HEAD` across `PLang/app/type/`, `PLang/app/module/ui/`, `PLang/app/module/` for the shapes `.Name == "..."`, `.Name is "..."`, `is { Name: "..." }`: exactly one new one, `Fluid.cs:165 format is { Name: "item" }` (codeanalyzer F3.v3). The branch REMOVED one of the same family — the hardcoded `Name == "list" || Name == "dict" || Name == "choice"` in `ToString` (F4.2, decision 833). Net: minus one, plus one. The carry F3.v3 is the only outstanding instance; it does not join a cluster that grew on this branch.

3. **The locked-core additions are all re-pinned under their decision numbers.** Spot-checked `_pinned-decisions` in the surface golden names from the summary: 774 (`kind<T>`), 776/777 (OwnX pattern), 783 (`text<separator>`), 788 (text unsealed), 814/831 (`OwnInternal`), 828/829 (`Kind(string)` by-name gone), 832 (file.read's Type slot), 844 (`IWriter.Bytes(byte[], kind)` — belongs to llm-writer, not landed here), 850 (`type.list[name]` bare kind — proposal only, carried). The three C# additions actually landed on this branch (OwnInternal, file.read.Type, F4.2 face) each have decision anchors in-code and in the surface golden per tester v1's N3 re-pin reading.

4. **`OwnInternal` is loop-free.** The chain `Internal => OwnInternal ?? kind.OwnInternal ?? Family._internal` terminates on `Family._internal` (a stored field), not on `Family.Internal` (which would recur). This matches the 777 `OwnX` pattern and makes the host kinds (`dict`, `list`, `*`) `Internal: true` without the kinded `{item, dict}` reading back through its own type. Verified at `type/this.cs` and the three host kind overrides.

5. **The deterministic kind-follow-up pin covers both halves of 814.** `KindFollowUpTests` has two tests: `AKindFollowUp_RefinesTheTypeOptionToTheKindedType` (M8 — Refine folds the kind in) and `AKindFollowUp_ForItem_OffersJson_NotTheInternalHostKinds` (M9 / N1 — the `!k.Internal` filter leaves dict/list/* out). Both canned-answer, both over `step.Pick.Take` without the LLM, both red under their mutation. The tester's v2 N1 is fully closed by 4db505aac.

6. **`AsTextLandsTextUncaptured` is in the plan's index.** 4db505aac added it to `start.goal` beside `AsTextKeepsText`. Tester v2's open "not in start.goal yet" is now closed.

## Findings

### F-SC-1 — the plan's Why names four kind/strict reds the branch does not deliver (minor, carry)

**Category:** architectural / plan-vs-code gap  
**Where:** `test/plan/kind-is-type/start.md:7–17` (Why), `start.goal:47–48` (stage-3 proof); `test/app/type/kind/strict/{SetAsImageGifStrictMismatch, SetAsImageStrictNoKind, SetAsTextSlashMarkdownNormalises, SetAsTextSlashMarkdownStrictUnverifiable}.test.goal`  
**Missed by:** none — tester v1 and codeanalyzer v3 both flagged it; routed to the architect, not yet closed  
**Issue:** The plan's Why cites "kind/strict ×4" as four of the sixteen plang-suite reds this branch exists to fix. `start.goal:48` calls `SetAsImageGifStrictMismatch` as the stage-3 proof ("a strict kind is checked"). On HEAD those four tests are still red; the coder's `baseline-failures.txt` notes "on runtime2 too (kind can't be checked)." So they aren't a regression — but the plan says they are proven at merge, and they aren't.  
**Impact:** Non-blocking. No test moved from green to red. But the plan's own stated proof is unfulfilled: a reader of the plan at merge sees "stage 3 fixes every family's kinds as types" and finds four of the Why's own motivating cases still red. The honest shape at merge is either (a) rescope the Why and `start.goal:48`, or (b) a decision carrying the four kind/strict reds explicitly to branch A with a reason, as the F3.v3 carry does.  
**Suggestion:** Architect call. Either delete the strict calls from `start.goal` and the "kind/strict ×4" claim from the Why with a note of what remains open, or add a line to the summary carrying them (and SetAsImageStrictNoKind, which is already red on runtime2) with the reason — the kind-strict check didn't move on this branch. docs can carry the note into the branch's done.list, but the plan edit needs the architect.  
**Disposition:** deferred.

### F-FL-1 — Fluid.cs:165 is the only new name-check on the branch (nit, informational)

**Category:** confirmation of a carried finding  
**Where:** `PLang/app/module/ui/code/Fluid.cs:165` — `format is { Name: "item" }`  
**Missed by:** none — codeanalyzer v3 F3.v3 named it; carried to branch A  
**Issue:** This is a confirmation, not a new finding. I checked whether F3.v3 is a lone case or the visible tip of a cluster the file-level reviewer wouldn't see. Diff `origin/runtime2..HEAD` across `PLang/app/{type,module/ui,module}` for `.Name == "..."`, `.Name is "..."`, `is { Name: "..." }`: exactly one new instance (`Fluid.cs:165`), and the branch REMOVED the hardcoded `list|dict|choice` name-list in `type.ToString` (F4.2). The F3.v3 carry is self-contained; the clean fix (a "rests as a structure" virtual on the kind) belongs with the #3 floor and F8.1 on branch A as the codeanalyzer named.  
**Disposition:** deferred (branch A, already recorded by codeanalyzer v3).

## What I didn't re-check

Per the character: the file-level OBP shape of stages 4–6 (codeanalyzer v2/v3), the mutation-coverage of the stages (tester v1/v2), the AuthGate/signing/sandbox/settings surface (security v2). I read each report in full and accepted the file-level verdicts after sampling the three load-bearing claims above (one door, OwnInternal loop-free, no new name-check cluster).

## Files read in full

Reviewer reports: codeanalyzer v2, v3; tester v1, v2; security v2; architect summary (decisions 762–850). Code: `data/this.cs:320–340` (Open door), `module/ui/code/Fluid.cs:145–175` (render fix), `PLang.Tests/.../KindFollowUpTests.cs` (both tests), `test/plan/kind-is-type/start.md` (Why), `start.goal:40–60` (stage-3 proof calls). Diffs: `origin/runtime2..HEAD` across `PLang/app/type/`, `PLang/app/module/ui/`, `.bot/kind-is-type/coder/baseline-failures.txt`.
