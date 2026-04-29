# Codeanalyzer v1 — Plan

## Branch context

`runtime2-builder-bootstrap` is the branch where the v2 PLang builder becomes self-hosting and a large diagnostics + module sweep landed on top. Diff against `runtime2`: 2347 files, ~69k insertions / ~6k deletions. The coder handover (`coder/v1/report.md`) only described 3 small "gap fixes" (`variable.set AsDefault`, `file.read ResolveVariables`, `TypeMapping` single→list auto-wrap); the branch actually contains the entire builder bootstrap + diagnostics squashed in commit `50351d8b` plus 5 follow-up commits.

I am NOT going to review every file. I am going to **deep-analyze the highest-risk new/changed C# files** and call out anything that needs the coder's attention.

## Targets (Tier 1 — deep dive, ~1968 lines)

These are either net-new code or heavily reworked, in central code paths.

| File | Lines | Why |
|------|------:|-----|
| `PLang/App/Utils/PlangTypeIndex.cs` | 196 | NEW — central CLR-type-name guard (key tripwire from commit `ada1901a`) |
| `PLang/App/Utils/TypeConverter.cs` | 394 | NEW — extracted from TypeMapping; full conversion logic |
| `PLang/App/Utils/TypeMapping.cs` | 554 | 708 lines changed — heavily reworked |
| `PLang/App/Catalog/this.cs` | 127 | NEW — catalog entry root, action registry |
| `PLang/App/Catalog/ExampleRenderer.cs` | 184 | NEW — renders action examples (Fluid-adjacent territory) |
| `PLang/App/modules/builder/validateResponse.cs` | 188 | 192 lines changed — validation gates, scalar checks, CLR-type guard call site |
| `PLang/App/modules/error/handle.cs` | 201 | 157 lines changed — recovery shape, MultipleRecovery fix, action-template ResolveDeep skip |
| `PLang/App/Actor/Context/Trace/this.cs` | 32 | NEW — per-build trace id |
| `PLang/App/Errors/ParamSnapshot.cs` | 27 | NEW — per-parameter snapshot on error |
| `PLang/App/Attributes/PlangTypeAttribute.cs` | 65 | NEW — supports the type system |

## Targets (Tier 2 — lighter scan, only flag clear issues)

| File | Lines diff | Why |
|------|------:|-----|
| `PLang/App/Catalog/{ActionSpec,ExampleSpec,TypeEntry,ExampleHelpers}.cs` | 153 | NEW — small catalog DTOs/helpers |
| `PLang/App/modules/builder/{BuildResponse,enrichResponse}.cs` | 36 | NEW — builder DTOs |
| `PLang/App/Utils/MimeTypes.cs` | 66 | NEW |
| `PLang/App/Errors/Error.cs` | 49 changed | ParamSnapshot wiring |
| `PLang/App/Actor/Context/this.cs` | 49 changed | Trace + Error wiring |
| `PLang/App/Variables/this.cs` | 106 changed | Resolve security-sensitive infra block |
| `PLang/App/Modules/this.cs` | 105 changed | Module registry |
| `PLang.Generators/LazyParamsGenerator.cs` | 49 changed | Per-parameter snapshot emit |
| `PLang/App/modules/llm/providers/OpenAiProvider.cs` | 79 changed | Dict→JSON Schema serialization |
| `PLang/App/modules/ui/providers/FluidProvider.cs` | 66 changed | Object .ToString() leak fix territory |
| `PLang/App/Debug/this.cs` | 282 changed | --debug flag wiring |
| `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` | 52 changed | Action core |

## What I will NOT review

- `system/` and `os/system/` PLang `.goal` and `.pr` files — those are LLM output / PLang code, not C#.
- `.build/` directories — generated.
- `Documentation/` — not code.
- Tests under `PLang.Tests/` — only spot-check if a Tier 1 file's behavior is unclear.
- Module providers (Db, Settings, Channels, etc.) outside Tier 1/2 list.

## Analysis passes per file

For each Tier 1 file, all 5 passes from the character spec:
1. **OBP compliance** — flag every violation against the 5 rules
2. **Simplification** — dead abstractions, over-parameterization, redundant checks
3. **Readability** — naming, length, flow
4. **Behavioral reasoning** — trace data origins; clone/copy family audit; generic catches that mask errors; rehydration fragility
5. **Deletion test** — could lines be removed without breaking anything?

For Tier 2 files, only flag clear issues from passes 1, 4, 5 (where the risk is highest).

## Outputs

- `v1/result.md` — full findings, per-file format from character spec
- `v1/verdict.json` — `{ "status": "pass"|"fail", "summary": "..." }`
- `v1/summary.md` — written before commit
- `summary.md` (bot root) — short cross-session paragraph
- `v1/changes.patch` — code-only diff (excluding .bot/) to `runtime2`

## Estimated severity gates

- `MAJOR ISSUES` if any silent error swallow, OBP rule-1 violation in central code, or behavioral hole that breaks the builder
- `NEEDS WORK` if multiple medium issues stack up
- `CLEAN` if findings are minor / cosmetic

## Open question for user (non-blocking)

The branch is big enough that I'm scoping to ~14 files of focused review. If you want the broader sweep (other modules, source generator changes), say so — otherwise I'll proceed with the scoped plan.
