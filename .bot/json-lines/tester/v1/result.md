# json-lines — the tester's line pass (tip 330718873, parent runtime2 0a178687b)

**Verdict: PASS, three minor findings.**

## Runs (`./dev.sh build` first)
- `./dev.sh test --force`: Modules 1,418 / Types 995 / Data 879 / Generator 191 / Runtime 1,390 green; Wire 612 with 6 red, the baseline's
  six names (SerializedString_…, TypedSnapshotString_…, NavigateAndEditCapturedVariable_…, PlangPath_AsSnapshotConvert_…,
  MidStackChain_…, ThrowTimeSnapshot_…). No new red name. The surface golden was re-pinned in the branch (55 lines).
- `plang --test '--test.setting.exclude=["llm"]'`: 308 total, 297 pass, 1 fail (`BareLiteralJudgedByForm`), 3 stale, 7 skipped. The baseline.
- Six `/module/llm` goals, saved (`OPENAI_API_KEY` unset, proxy at a closed port): 6/6; live (`--test.setting.llm=live`): 6/6.
  `git status` clean after each: no `.llm.json` changed.

## Mutations (each reverted; tree clean)
| Change | Mutation | Red |
|---|---|---|
| errors.jsonl in its own kind (`window/this.cs` Kept) | back to the json writer | ErrorsKeptTests (1) |
| cursor written as text (`headless/this.cs:205`) | back to json | **nothing** (finding 1) |
| blank line skipped (`jsonl/this.cs:52`) | removed | ABlankLine_IsSkipped + 3 |
| CR trimmed (`jsonl/this.cs:51`) | removed | nothing: json reads a trailing CR as whitespace, an equivalent mutation |
| pasted json laid on one line (`jsonl/writer.cs:61`) | always re-lay | none red the other way; always re-lay is output-equal, so not mutated further; removing the re-lay is covered by JsonContent_IsOneLine / …InADict (red when the condition is forced off, not shown here) |
| layout read (`json/this.cs:54`) | always indented | Json_IsCompact_WhenTheSettingSays, TheLayout_IsSetAlongItsPath |
| layout default (`json/setting/this.cs:14`) | compact | Json_IsFormatted_ByDefault, TheLayout_IsReadAlongItsPath |
| llm tool result as text (`OpenAi.cs:534`) | back to json | **nothing** (finding 2) |

## Findings
1. (minor) **`HeadlessCursorTests` does not pin the production line.** Its `Line()` repeats `list["text"].kind.Encode(...)` itself, so
   headless reverted to the json writer leaves both tests green. Known (headless needs Chromium); the plan's "red with the old writer"
   does not hold for the cursor. Suggest: put the encode in one member the headless and the test both call.
2. (minor) **The llm tool result as text is pinned by no test.** `OpenAi.cs:534` back to the json kind leaves QueryToolTests and
   LlmAnswersFileTests green. Suggest: a tool returning a leaf and a structure; assert the message content is the bare text / compact json.
3. (minor) **start.goal names tests that don't exist:** `WindowErrorsTests (ARecord_IsOneLine)` (the real test is
   `ErrorsKeptTests.AnErrorThatCameUp_IsKept_AsARecordPerLine`) and `HeadlessCursorTests (TheCursor_IsOneLine_EndedOnce)` (real:
   `TheCursor_IsOneCompactLine_WithJsonFormatted`). Fix the names in start.goal.

## Gaps looked for
CR LF, blank line, bad line (naming its number), pasted formatted json in a record and in a dict, set of the layout along its path then
write, `%!app.type.kind.json%` read: each has a C# test (JsonlKindTests, JsonLayoutSettingTests) or the plang LayoutSetting goal; no gap found.
Not run: the cursor in a real headless browser (windows list).
