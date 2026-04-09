# v1 Review Summary

Tester v1 found 3 major false-greens and 4 minor findings. All addressed:

1. **GoalCall PrPath not verified** (major) — Fixed: test now asserts `PrPath == "/.build/dosomething.pr"`. Also discovered a production bug: `existsResult.Value is PLangPath` → `existsResult is PLangPath` (file.Exists returns PathData which extends Data, so Value was null but the result itself IS a PathData).
2. **SaveApp content not verified** (major) — Fixed: reads back, deserializes, checks Id and Version.
3. **SaveGoals content not verified** (major) — Fixed: reads back, deserializes, checks Name and Steps.Count.
4. **GoalsSave error guards** (minor) — Fixed: added EmptyGoalsList and NoPrPath tests with Error.Key checks.
5. **App corrupt JSON** (minor) — Fixed: added GetApp_CorruptJson test with Error.Key check.
6. **CorruptPrFile warnings** (minor) — Fixed: asserts Warnings contains "CorruptPrFile" key.
7. **MergeFrom duplicate text** (minor) — Fixed: added DuplicateStepText test verifying consumed HashSet behavior.
