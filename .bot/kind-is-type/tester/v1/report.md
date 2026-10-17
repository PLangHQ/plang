# kind-is-type — tester v1

Scope: the code since 82d8866ae (stage 4, 814/831, file.read's Type + Build, stage 6, the builder's stage 5),
plus the coder's render fix 2e83d21ff. Compared against the coder's baseline (98262938e, updated in 2e83d21ff).
HEAD reviewed: 2e83d21ff. Every result below is from a forced build (`rm -f .devsh-stamp*; ./dev.sh build`).

**Verdict: FAIL (tests, not code).** No new red name against the baseline. Two confirmed false greens:
a behaviour of the plan can be removed and every test stays green (F1 for file.read's run, F2 for the
builder's kind follow-up). F1 is now pinned by a plang test I added. F2 needs a C# test from the coder or
the builder.

## Suites

| suite | result | against the baseline |
|---|---|---|
| C# (`./dev.sh test`, 63ef49a5d) | Modules 1286/0, Types 852/0, Wire 533/6, Data 842/0, Generator 188/0, Runtime 1066/0 | the 6 Wire snapshot/resume tests only; the timing test passed; TheFilePage green |
| C# (2e83d21ff, after the render fix) | Modules 1287/0, Types 852/0, Wire 533/6, Data 842/0, Generator 188/0, Runtime 1066/0 | the 6 Wire only; the timing test passed |
| plang (`cd test && plang --test`, 63ef49a5d), run twice | 293 / 261 pass / 17 fail / 12 stale / 3 skipped, both runs the same names, git clean after | new: RenderFile, RenderInclude (known, decision 849) |
| plang (2e83d21ff) | 294 / 264 pass / 15 fail / 12 stale / 3 skipped (294 includes my new test) | none new; RenderFile and RenderInclude green |
| the plan's folder (`test/plan/kind-is-type`) | 18/18 (17 + mine) | — |

The builder itself was checked first: a throwaway goal using set, foreach + call, if + call and assert was
built with `"cache":"skip"`; the `.pr` was correct and the test passed; the throwaway was deleted. (This
host's store had no decider key, a 403; re-saved through `test/SetDeciderKey.goal`, piped, never printed.)

## Mutations (stages 4–6), each reverted, git clean after

| # | mutation | caught by |
|---|---|---|
| M1 | json parses at `Value()` instead of resting (`source.cs:217`) | plang PassedOnUnread; C# Render_AJsonFile_IsItemOfKindJson, AUrl_DecodesByItsContentType, UrlServedApplicationJson_DecodesAsItemOfKindJson |
| M2 | a use opens but doesn't narrow (`data/this.cs:330`) | plang ReadIntoIsADict, ArrayIsAList, JsonIsItemsKind; C# TwoGets_ParseOnce, Navigation_ParsesJsonIntoAPlangDict_AndNarrowsAfterTheRead, PropertyAccess_OnItemOfKindJson_YieldsPlangNumber_ParsesOnce, Foreach_OverAJsonArray_YieldsPlangNumbers_AndNarrowsToList |
| M3 | `is` asks `Value()`, not `Open()` (`Operator.cs:182`) | plang JsonIsItemsKind only (see N1) |
| M4 | `Internal` stops asking the kind (`type/this.cs:736`) | HostKinds_AreInternal_JsonIsOffered |
| M5a | file.read ignores Type at run (`read.cs:32`) | **nothing** (F1); now my AsTextLandsTextUncaptured |
| M5b | Build, literal path, ignores Type | Read_AsText_Build_PublishesTheAsType_SoTheCaptureTakesText |
| M5c | Build, variable path, ignores Type | Read_AsText_VariablePath_Build_PublishesText |
| M6 | the face `name<kind>` only for list/dict/choice again (F4.2 reverted) | ScopeTests.AKnownVariableOfTheRightType_Passes; PickListTests ×3 (prompt C, stage-two questions) |
| M7 | the text face drops `kind.IsText`, the name floor only (`source.cs:115`) | Render_AJsonFile_IsItemOfKindJson |
| M8 | the builder's `Refine` is a no-op (`pick/list/this.cs:646`) | **nothing** (F2) |
| R | the render fix reverted (`Fluid.cs`, `format != null`) | Render_HtmlFile_LandsAPlainText; plang RenderFile, RenderInclude |

## Findings

### F1 — false green: file.read's Type at run (decision 831/832)
`FileHandlerTests.Read_AsText_LandsTheContentAsText_NotAFileReference` passes with the Type branch of
`Read.Start` removed (M5a). It asserts `IsTypeOf<text>` on `await result.Value()`, and a `{file, md}`
reference's `Value()` already materializes to its content, a text, so the test can't tell "landed a text"
from "landed a file reference", which is the behaviour it names. The plan's AsTextKeepsText can't see it either:
its capture's `variable.set` carries `Type=text` (`astextkeepstext.test.pr:71–79`) and converts a file reference.

**Added:** `test/plan/kind-is-type/module/file/read/AsTextLandsTextUncaptured.test.goal`
(`read notes.md as text` with no capture; asserts `%!data!type.name%` is `text`). Built fresh, `.pr` read
(file.read with `Type=text`, nothing else); green on HEAD, red under M5a (`Expected "text", Actual "file"`).
**For the coder:** make the C# test check what the read landed before it is opened (the Data's own
type or `Peek()`), not what `Value()` materializes to.

### F2 — false green: the builder's kind follow-up (stage 5, ruling 10; decisions 812/816)
With `Refine` a no-op (M8), every C# test stays green (PickListTests 8, PickOptionTests 19,
OptionQuestionTests 9, ConfirmTemplateTests 2, LineTwinTests 1, the full suites) and so does the plan's
stage 5 (5 tests + the guard): they run their committed `.pr`s and never call the builder. The break is
real: a fresh build (`"cache":"skip"`) of `set %n% = "1.5" as decimal` under M8 wrote `Type="number"`;
reverted, the same build wrote `{"name":"number","kind":"decimal"}`. Nothing deterministic pins the
`KindOf` question (`pick/list/this.cs:603–622`), `KindsOffered` (`:635–642`, including the `!k.Internal`
filter that 814 exists for) or `Refine` (`:646–652`).
**For the coder or the builder:** a C# test over `pick.list` with a canned answer (as OptionQuestionTests
does for Option): a step coercing to `number` gets a KindOf question offering number's kinds and `none`
without the host kinds; answering `decimal` leaves the option `{number, decimal}`; answering `none` leaves
`number`; a `list` pick offers types as its element. It must go red under M8.

### Notes (not blocking)
- **N1** `is` as the first use of an unread json is pinned once, by the plan's JsonIsItemsKind (ReadIntoIsADict
  and ArrayIsAList read a member first). No C# test; one catcher is enough, recorded.
- **N2** PassedOnUnread pins "byte for byte", not "still unread after the write". A parse that wrote the raw
  bytes back would pass. M1 is caught by four tests, so not a gap in practice.
- **N3** The re-pins read: `surface_golden.txt` moves only members named by decisions (814/831 OwnInternal,
  828 navigator gone, 829 `Kind(string)` gone, 832 file.read's Type, 783 `text<separator>`);
  `formal_errors.json` gains `Type` in file.read's list; `pick_golden.json`, all 42 step→action picks the same
  before and after (82d8866ae vs HEAD), only teaching and prompt text moved. No re-pin captures a worse answer.
- **N4** Under M1's full sweep `ReadsBesideWrites_NeitherThrowNorStayStale` (Runtime) also went red; it was green in
  both clean sweeps. Concurrency noise, not counted.

## OPEN (not findings)
- **The plan's stage-3 proof is red.** `start.goal` stage 3 calls `/app/type/kind/strict/SetAsImageGifStrictMismatch`
  ("a strict kind is checked") as its proof; it fails (`Expected "gif", Actual "png"`), with its three siblings, all
  in the baseline as red on runtime2 too. Decision 777 had 3 strict tests fixed at stage 1. So the plan's index
  claims a behaviour that isn't proven at merge. The architect should decide: re-scope the line or fix it.
- **The render fix** (2e83d21ff, after the codeanalyzer's v2) decides by the format's parent name:
  `format is { Name: "item" }` (`ui/code/Fluid.cs:165`). Tested (R above), but a name check of the kind F8.1 is
  about; for the codeanalyzer's eye, not a test finding.
- Baseline reds unchanged: C# 7 (the 6 Wire, the timing test, which passed), plang 15.
