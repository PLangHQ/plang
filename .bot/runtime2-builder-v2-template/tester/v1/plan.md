# Tester v1 Plan — UI Module (Template Rendering)

## Branch: runtime2-builder-v2-template
## Coder version: v1

## What I'm testing
The UI module: `render.cs` action handler, `FluidProvider.cs` (Liquid template engine), `ITemplateProvider.cs` interface. 29 C# tests + 5 PLang test goals.

## Approach

1. Run full C# test suite — record pass/fail
2. Run Cobertura coverage on UI module files
3. Analyze test quality using deletion test, false-green hunting, coverage gaps
4. Check PLang test existence and quality
5. Write test-report.json and verdict

## Initial findings from coverage

- **FluidProvider.Render**: 94.8% line coverage, 85% branch — good
- **CallGoalTagAsync**: 60.7% line, 37.5% branch — success path and empty-name path uncovered
- **Render.Run()**: 0% — all tests bypass the action handler, calling provider directly
- **LooksLikeFilePath**: 0% — auto-detect (IsFile=null) never exercised
- **GetTemplateBaseDir**: mostly uncovered — no test sets goal context
- **IOException catch**: 0% — never triggered

## Suspected false greens
- 5 callGoal tests all test the same "goal not found" path with different names
- `Render_Include_MissingPartial_ReturnsError` only asserts `result != null`
- `Render_CustomProvider_IsUsed` calls stub directly, doesn't test provider resolution
- `Render_DataObject_ExposesValueNotWrapper` tests string (which always works), not actual wrapper unwrapping
