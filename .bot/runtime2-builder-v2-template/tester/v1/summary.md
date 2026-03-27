# Tester v1 Summary — UI Module (Template Rendering)

## What this is
Test quality analysis of the UI module (Liquid template rendering via Fluid). The coder wrote 29 C# tests and 5 PLang test goals. All 1886 C# tests pass. This analysis hunts for false greens and coverage gaps.

## What was done

### Test run
- **C# tests**: 1886 total, 1882 passed, 0 failed, 4 skipped (skips are unrelated sub-engine tests)
- **PLang tests**: 5 goals written, not yet built (no .pr files — requires LLM builder)

### Coverage (UI module files)
| Component | Line % | Branch % |
|-----------|--------|----------|
| FluidProvider.Render | 94.8% | 85% |
| CallGoalTagAsync | 60.7% | 37.5% |
| PlangFileProvider | 92.3% | 70% |
| PlangFileInfo | 81.2% | 100% |
| LooksLikeFilePath | 0% | 0% |
| GetTemplateBaseDir | 0% | 0% |
| Render.Run() | 0% | — |

### Findings (3 major, 5 minor)

**Major:**
1. **callGoal false greens** — 5 tests claim to test different callGoal behaviors (success insertion, non-string return, parameter passing) but all test the identical "goal not found" path. The success path, empty-name path, and exception catch are all uncovered. 37.5% branch coverage.
2. **LooksLikeFilePath 0%** — Auto-detect (IsFile=null) is the default user experience. No test exercises it.
3. **GetTemplateBaseDir 0%** — Goal-relative include resolution never tested because no test sets a Goal on context.

**Minor:**
4. `Render_Include_MissingPartial_ReturnsError` — only asserts `result != null` (passes for any return)
5. `Render.Run()` at 0% — tests call provider directly, bypassing action handler
6. `Render_DataObject_ExposesValueNotWrapper` — tests string (trivial), not actual wrapper unwrapping
7. IOException catch path never triggered
8. PLang tests not yet built

## Code example — false green pattern

```csharp
// Test NAME: "Render_CallGoal_NonStringReturn_ConvertedToString"
// What it ACTUALLY tests: goal not found error (identical to 4 other tests)
[Test]
public async Task Render_CallGoal_NonStringReturn_ConvertedToString()
{
    var ctx = CreateContext();
    var action = new Render { Context = ctx, Template = "{% callGoal 'NumberGoal' %}", IsFile = false };
    var result = await _provider.Render(action);
    // This accepts ANY result — success with error text OR error Data
    if (result.Success) { /* checks for "[Error:" */ }
    else { /* checks result.Error != null */ }
}
```

This test would pass even if callGoal was completely broken and returned `Data.Ok("")`.

## Verdict: FAIL — needs fixes

Recommend sending back to **coder** to address the 3 major findings before proceeding to security analysis.
