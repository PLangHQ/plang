# Plan — codeanalyzer v3 on fix-stepvartypes-incremental

## Why v3

User explicitly asked me to apply the OBP shape-smell lens — particularly the new 5th smell (producer raw, consumers transform) we proposed in v2's character work — across the **whole branch surface**, not just the delta since v2. v1 reviewed 8 files in narrow scope; v2 re-validated those 8 post-merge. The branch is actually 12 production C# files / 59 commits beyond `origin/runtime2`. v3 widens the lens.

Also re-validate: does the small post-v2 commit (`9af7fd8b2` test.report nested-test suppression) hold up?

## Scope

All branch-unique production C# (12 files), categorized by review status:

| File | LOC delta | v1/v2 covered? |
|------|-----------|-----|
| `PLang/app/modules/test/run.cs` | 73 | partially (post-merge `.ToString()` only) |
| `PLang/app/modules/llm/code/OpenAi.cs` | 58 | yes (v1) |
| `PLang/app/modules/test/report.cs` | 38 | yes (v1+v2) — has open LOWs |
| `PLang/app/tester/Timings.cs` | 26 | yes (v1) |
| `PLang/app/tester/Run.cs` | 11 | yes (v1) |
| `PLang/app/modules/this.cs` | 10 | yes (v1) |
| `PLang/app/types/path/this.cs` | 9 | **NO — new in v3** |
| `PLang/app/types/Conversion.cs` | 9 | **NO — new in v3** |
| `PLang/app/tester/Timing.cs` | 8 | yes (v1) |
| `PLang/app/modules/test/discover.cs` | 8 | **NO — new in v3** |
| `PLang/app/modules/builder/BuildResponse.cs` | 7 | yes (v1) |
| `PLang/app/types/path/this.Derivation.cs` | 6 | **NO — new in v3** |
| `PLang/app/modules/condition/code/Default.cs` | 6 | **NO — new in v3** |
| `PLang/app/formats/this.cs` | 2 | **NO — new in v3** |
| `PLang/app/tester/File.cs` | 4 | **NO — new in v3** (producer of Path/PrPath strings) |

## Passes

Standard 5-pass, but with explicit Pass 1b run against the **proposed** 5-item OBP smells list (item 5 = producer raw, consumers transform) — even though it's not yet in CLAUDE.md, the user just confirmed it's the rule and asked me to apply it.
