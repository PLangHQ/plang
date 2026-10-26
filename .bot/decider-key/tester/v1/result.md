# decider-key — the tester's line pass (tip aa2e59b01, parent runtime2 493b157df)

**Verdict: PASS, no findings.**

## Runs (`./dev.sh build` first)
- `./dev.sh test --force`: Modules 1,422 / Types 1,000 / Data 879 / Generator 191 / Runtime 1,390 green; Wire 612 with 6 red, the
  baseline's six names (SerializedString_…, TypedSnapshotString_…, NavigateAndEditCapturedVariable_…, PlangPath_AsSnapshotConvert_…,
  MidStackChain_…, ThrowTimeSnapshot_…). No new red name.
- `plang --test '--test.setting.exclude=["llm"]'`: 308 total, 297 pass, 1 fail (`BareLiteralJudgedByForm`), 3 stale, 7 skipped. The baseline.
- Six `/module/llm` goals: saved (`OPENAI_API_KEY` unset, proxy at a closed port) 6/6; live (`--test.setting.llm=live`) 6/6.
  `git status` clean after: no `.llm.json` changed.

## Mutations (each reverted; tree clean)
| Change | Mutation | Red |
|---|---|---|
| the decider asks its key (`TypeSafe.cs:31`) | use `setting.Key` as it was | NoOneToAsk_FailsNamingBothWays_AndSendsNothing |
| the asked key is saved (`IKey.cs:38`) | not saved | AMissingKey_IsAskedOnce_Saved_AndNeverShown (MissingDeciderKeyTests and MissingKeyTests) |
| why no one could be asked rides as the cause (`IKey.cs:33`) | cause dropped | NoOneToAsk… (ChannelEof) and MissingKeyTests.NoOneToAsk_FailsNamingBothWays |
| the ask is secret (`IKey.cs:26`) | `Sensitive` false | AMissingKey_IsAskedOnce_Saved_AndNeverShown, both classes |

## First contact (TYPESAFE_API_KEY unset, no saved decider key, stdin `< /dev/null`, proxy at a closed port)
Scratch folder with one goal `Start` (`- write out "hello"`), `plang build --app.setting.create=true` (the folder is no app yet):

```
Building path: …/firstcontact
Found 1 goals
Building goal: Start
Building needs a TypeSafe key for the decider. Paste it:
  Build error in Start: Building needs a TypeSafe key for the decider, and no one gave one: set %!llm.decider.setting.key% or the TYPESAFE_API_KEY environment variable.
MissingDeciderKey (401): Building needs a TypeSafe key for the decider, and no one gave one: set %!llm.decider.setting.key% or the TYPESAFE_API_KEY environment variable.
  - Channel 'input' has no interactive answerer (stream EOF)
  at /system/builder/BuildGoal/Decide.goal:22
    - llm.decider State=%state%, Question=%questions%, write to %answer%
```
Exit 1. It names the setting and the variable; no 403, no stack trace (0 lines of `   at`), no request went out (the proxy was closed, and the
failure is the key's, not a connection's). No key was printed or saved.
