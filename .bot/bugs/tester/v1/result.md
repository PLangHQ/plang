# bugs — the tester's line pass (tip 3c845fcf8, parent runtime2 3a29633f8)

**Verdict: PASS, two minor findings.**

## Runs (PlangConsole rebuilt first)
- `./dev.sh test --force`: Modules 1,376 / Types 973 / Data 879 / Generator 191 / Runtime 1,383 green; Wire 604 with 6 red,
  the baseline's six names (SerializedString_…, NavigateAndEditCapturedVariable_…, PlangPath_AsSnapshotConvert_…,
  TypedSnapshotString_…, MidStackChain_…, ThrowTimeSnapshot_…). No new red name.
- `plang --test '--test.setting.exclude=["llm"]'`: 305 total, 294 pass, 1 fail (`BareLiteralJudgedByForm`), 3 stale (no .pr),
  7 skipped. The baseline.
- The six `/module/llm` goals: saved, `OPENAI_API_KEY` unset and `HTTPS_PROXY` pointing at a closed port: 6/6 pass (~0.3 s
  each). Live (`--test.setting.llm=live`, the container's key): 6/6 pass (~2.1–2.7 s each, so real calls). After each, `git status`
  clean: no `.llm.json` changed.

## Removed tests (8e6888aba)
13 `[Test]` lines gone, all N1's export: `ConsentedExportMeansTheStepHoldsTheKeyTests.cs` (4: the yes hands the step the
key, the key as text saved to a file / posted to a url, a yes is per call), `IdentityHandlerTests` (Export_NonExistentName,
Export_ReturnsFullIdentity, Export_NullName, ABindingOnTheInputsAsk…, AStepSettingTheAskAnswer…), `IdentityPrivateKeyLeakTests`
(Export_WithTheActorsYes…, TheExportQuestion_SaysWhatAYesMeans). Every one asserts a key handed out after a yes: behaviour gone
(`Default.cs:198`, export refuses before any ask). Replaced by `Export_Refuses_UntilCriticalActions`,
`Export_WithoutTheActorsYes_IsRefused`, `Export_NullName_OnAnAppHoldingNone_IsRefused`, `TheExportQuestion_IsNeverAsked`.
No other test in the build went.

## Mutations (each reverted; tree clean)
| Change | Mutation | Red |
|---|---|---|
| Saved matches by request (`Saved.cs:46`) | match any | AChangedPrompt_IsNotServedTheOldAnswer, TwoTestsInParallel_EachReadsItsOwnFile |
| Saved keeps what it asked (`Saved.cs:71`) | keep all | same two |
| goal channel runs in the writer's flow (`channel/type/goal/this.cs:~122`) | old: `Call(data, Actor.Context)` | GoalChannelRaceTests ×2; plang `EachWriteSeesItsOwnMessage` and `ParallelWritesSeeTheirOwn` both FAIL (PlangConsole rebuilt for it) |
| named actor runs in the asker's flow (`actor/list/this.cs:38`) | `then(this[name].Context)` | NamedActorCallRaceTests |
| path.Holds separator (`path/this.cs:185`) | StartsWith(folder) | ValidatePathTests ×2 |
| Stands on its own folder (`permission/this.cs:162`) | any standing grant answers | StoreStandingProbeTests (1 of 3) |
| Add answers its save (`permission/this.cs`, `Add`) | drop the answer | GrantKeepTests ×2 |
| a bug in the ask is no denial (`permission/this.cs` first catch) | catch all | ConsentAskTests ×2 |
| frame bound (`Bitmap.cs:55`) | unbounded | WindowFrameTests ×5 |
| a setting path as goal (`start/this.cs` IsSettingPath) | off | StartRefusalTests ×1 |
| module.Remove answers false (`module/this.cs` Drop) | true | ModuleRemoveTests ×1 |
| export refusal key (`identity/code/Default.cs:201`) | other key | Export_Refuses_UntilCriticalActions + 2 more |
| channel born in its list (`channel/list/this.cs:165`, `Born`) | check off | **nothing** |

## Findings
1. (minor) **`ChannelBirthTests` does not pin "born in its list, never after".** The five tests are fast-apps' size tests with the
   new constructor (`ad0caedd7` only adds the list argument). With `Born`'s check off (`channel/list/this.cs:165`), all five
   stay green. Nothing asserts that registering a channel born of another actor's channels is refused, or that a channel's actor is
   its list's. Suggest: register a channel born of the user's channels in the system's list; expect the refusal; and assert
   `channel.Actor == list.Actor` for a registered one.
2. (minor) **The plan names `LlmAnswersFileTests (Live_WritesNothing)` and `TheKey_NeverLandsInTheFile`; neither exists.** The
   class has four tests (ANewQuestion_IsAskedOnce_ThenRead, AChangedPrompt…, TheKey_IsNeverInTheFile, TwoTestsInParallel…).
   "live asks OpenAI and writes nothing" has no C# test: only the gate's live run proves it (clean `git status` above). Suggest:
   a test registering `Live` (or `test.setting.llm=live`) with the mock OpenAI: one call, no file written; or fix the plan's name.

Not mutated (outside the sample asked): JsonReaderTests / TypeReaderErrorTests, OsTrustTests (Windows), APatchBeforeTheFirstFrame,
PersistedGrantCannotOverrideStoresTests, ExecutorTests.
