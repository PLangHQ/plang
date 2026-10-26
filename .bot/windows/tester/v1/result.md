# windows — the tester's line pass (tip 001a8e434, parent runtime2 b4a549d1c)

**Verdict: FAIL — one confirmed false green: the plan's plang test `EachGoalIsTheAppsOwn`.** Everything else holds.

## Runs (Linux; `./dev.sh build` first)
- `./dev.sh test --force`: Modules 1,432 / Types 1,008 / Data 880 / Generator 191 / Runtime 1,401 green; Wire 612 with 6 red, the baseline's
  six names (SerializedString_…, TypedSnapshotString_…, NavigateAndEditCapturedVariable_…, PlangPath_AsSnapshotConvert_…, MidStackChain_…,
  ThrowTimeSnapshot_…). No new red name.
- `plang --test '--test.setting.exclude=["llm"]'`: 309 total, 298 pass, 1 fail (`BareLiteralJudgedByForm`), 3 stale, 7 skipped. The baseline.
- Six `/module/llm` goals: saved (`OPENAI_API_KEY` unset, proxy at a closed port) 6/6; live (`--test.setting.llm=live`) 6/6. `git status` clean
  after: no `.llm.json` changed. llm.query's messages render (RequiredVariableTests, below).

## Mutations (each reverted; tree clean)
| Change | Mutation | Red |
|---|---|---|
| goal / step / action start through their own events alone when a system goal's | all three back to the app's | OnEventTests: BeforeEachGoal_…, BeforeEachStep_…, BeforeEachAction_… (each level alone: goal level red on BeforeEachGoal) |
| same, as the plan's plang test | all three back (PlangConsole rebuilt fresh, 0 errors) | **nothing: `/plan/windows/app/event/EachGoalIsTheAppsOwn` passes** |
| debug names a system goal by address (`debug/this.cs`, the bind) | off | DebugAttachTests.ASystemGoal_NamedByItsAddress_IsDebugged |
| (`debug/setting/this.cs`, `Matches` by address) | off | same test |
| a rooted child is under the path (`path/file/this.Derivation.cs:42`) | not trimmed | PlangFormTests.Combine_ARootedChild_IsUnderThePath_InBothForms |
| Typed.Parent of "/x" (`this.Typed.cs`) | "." not "/" | PlangFormTests.Parent_IsTheFolderBeforeTheLastSlash |
| Typed.Extension keeps a dot in a folder name (`this.Typed.cs`) | dot taken anywhere | PlangFormTests.WithExtension_ChangesTheLastPartsExtension |
| the store's folder 0700 (`store/sqlite/this.cs`) | 0775 | StoreFolderModeProbeTests.ASettingStoresFolder_HasNoGroupOrOtherBits |
| `[IsNotNull]` runs a method once (`variable/code/Method.cs`, IsLookup) | true | RequiredVariableTests.ARequiredVariableThatCallsAMethod_RunsItOnce |
| llm.query renders its messages (`llm/query.cs`, Messages) | no render | RequiredVariableTests ×2 (AMessagesVariables_AreSentAsWhatTheyHold, …NotSet_FailsTheRead…) |

## Windows-only: cannot go red here
Named, not mutated (on Linux the OS separator is "/", so the Windows forms are equal):
- `GrantMatchTests`: OnWindows_AStar_StopsAtEitherSeparator, OnWindows_AnExactGrant_IgnoresCase, OnWindows_AGlobGrant_IgnoresCase (the Linux
  `AStar_StaysInOnePart`, `AUrlGrant_KeepsCase` pass but the glob's second separator and the scheme-by-OS comparison are no-ops here).
- `PlangFormTests.AnOsLocationWrittenWithSlashes_ShowsItsPlangForm`, `FileUrlTests` (line 29), `RootComparisonKeyingTests.PathEquals_…OnWindows…`,
  `SubAppStoresShapesProbeTests` (180), `LinkSpotTests` (133, junction), `StoresFolderTests` (113, short name).
- The LF line ends (`goal/this.cs` ToString, `goal/this.Item.cs` .pr NewLine, `terminal/process` Write): `AppendLine` / `Environment.NewLine` are LF on
  Linux, so no test here tells the old from the new. The .pr byte-equal check is the Windows run.
- The Linux-marked tests (BirthTests, ProgramTests, PipeTests, PermissionTests, Unpack*) run here and pass.

## Finding
1. (**major**, confirmed false green) **`test/plan/windows/app/event/EachGoalIsTheAppsOwn.test.goal` goes green without its change.** With the goal,
   step and action `Level` all back to the app's (`goal/this.cs`, `goal/step/this.cs`, `goal/step/action/this.cs`), PlangConsole rebuilt fresh (0
   errors, dll newer than the edit), `plang --test plan/windows` still passes 1/1. The C# `OnEventTests` pin the same behaviour and do go red, so the
   change is covered; the plan's plang proof is not. Why it passes, my reading: the step `set %name% = %!goal.name%` runs after `call goal Mine`,
   and `assert %last% equals "Mine"` holds if the shortcut goal's run is not heard or had run before the binding; the test never makes the system
   goal run after the bind. Suggest: make a system goal run while the binding is live, and assert it was not heard (e.g. count the fires; or call
   `/system/…` explicitly), then confirm it goes red with the `Level` lines back.

## Gaps looked for
- A doubly nested event under a system goal: probe (a test added to OnEventTests, then removed): app goal Run → /system/Sys → /system/Sys2 → app goal
  Mine, "before each goal" bound: it fired for Run and Mine only (trail ",Run,Mine"). Holds.
- A path Combine with a rooted child: covered by `Combine_ARootedChild…` (red when the trim goes).
- A URL glob: `AUrlGrant_KeepsCase` covers exact and glob case on Linux; the only Windows-specific part, a `*` also stopping at "\" in a URL's path, is
  harmless for URLs and not testable here.
