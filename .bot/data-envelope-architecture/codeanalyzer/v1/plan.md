# Code Analyzer v1 — Plan

## Scope
Analyze all 17 changed files in the `data-envelope-architecture` branch (vs `runtime2` merge base). Three-pass analysis: OBP compliance, simplification, readability.

## Files to analyze (production code)
1. `PLang/Runtime2/Engine/Memory/Data.cs` — core Data class (partial)
2. `PLang/Runtime2/Engine/Memory/Data.Result.cs` — result/error concern
3. `PLang/Runtime2/Engine/Memory/Data.Navigation.cs` — navigation concern
4. `PLang/Runtime2/Engine/Memory/Data.Envelope.cs` — envelope/transport concern
5. `PLang/Runtime2/Engine/Memory/MemoryStack.cs` — variable storage
6. `PLang/Runtime2/Engine/Types/this.cs` — centralized type knowledge
7. `PLang/Runtime2/Engine/View.cs` — serialization view attributes
8. `PLang/Runtime2/Engine/this.cs` — Engine root (changes only)
9. `PLang/Runtime2/Engine/Context/PLangContext.cs` — context (changes only)
10. `PLang/Runtime2/Engine/Goals/Goal/Steps/Step/Actions/Action/Methods.cs` — action execution
11. `PLang/Runtime2/actions/convert/fromJson.cs` — fromJson handler
12. `PLang/Runtime2/GlobalUsings.cs` — global aliases

## Files to analyze (test code — lighter pass)
13. `PLang.Tests/Runtime2/Memory/DataTests.cs`
14. `PLang.Tests/Runtime2/Memory/MemoryStackTests.cs`
15. `PLang.Tests/Runtime2/Types/EngineTypesTests.cs`
16. `PLang.Tests/GlobalUsings.cs`

## Approach
- Production code: full 3-pass (OBP, simplification, readability)
- Test code: readability + pattern consistency only (tests don't need OBP analysis)
- Engine root and PLangContext: only analyze the diff, not the full file (they were modified, not created)

## Output
- `v1/result.md` — full per-file analysis
- `v1/summary.md` — session summary
- Root `summary.md` — cross-session summary
