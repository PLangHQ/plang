# Tester v2 Summary — Phase 2 Context + Lazy Derivation

## What this is

Test quality analysis of coder v2 — Phase 2 adds Type context navigation (Kind, Compressible, ClrType), Data late-bound context, Variables/PLangContext context stamping, and Engine.Types.KindOf(). 23 new tests added across 3 test files.

## What was done

1. **Ran full test suite** — 1324 pass, 0 failures, no regressions (95 new tests since Phase 1).
2. **Analyzed Phase 2 tests** — context propagation chain well-covered, KindOf tests solid.
3. **Found a real bug** — experimentally verified that `Add()` doesn't update `_allKinds` or `_mimeToKind`, breaking `KindOf()` for dynamically added extensions. The existing Add test is a false green because it only checks `Kind()`/`Mime()`, not `KindOf()`.
4. **Tracked v1 findings** — all 4 major/critical v1 findings carry forward unfixed, now with higher impact since Phase 2 actively uses the affected paths.

## Findings: 8 total (2 critical, 3 major, 3 minor)

### New in v2

| # | Severity | Issue |
|---|----------|-------|
| 1 | critical | **Add() doesn't update _allKinds/_mimeToKind** — KindOf() can't find dynamically added kinds. Real bug, verified. |
| 5 | major | **Methods.cs context stamping untested** — critical runtime code path with no test |
| 6 | minor | **DynamicData Type derivation broken without explicit type** — Type is null while Value is non-null |
| 7 | minor | **Lazy derivation test can't distinguish context path from fallback** — both produce same result for standard types |

### Carried from v1 (unfixed)

| # | Severity | Issue |
|---|----------|-------|
| 2 | critical | Kind(null)/Mime(null) crash — now more severe as Phase 2 adds callers |
| 3 | major | Name() backtick — now hits Data.Type lazy derivation path |
| 4 | major | BuilderNames()/ComplexSchemas() still zero tests |
| 8 | minor | No PLang .goal tests — now more important since Phase 2 changes runtime behavior |

## Verdict: needs-fixes

Finding #1 is a real bug, not just a test gap. The Add → KindOf pipeline is broken for custom extensions.

## Code example — the Add() bug

```csharp
// After this:
engine.Types.Add(".custom", "custom-kind", "application/custom");

// These work (read _extensionToKind/_extensionToMime directly):
engine.Types.Kind(".custom")    // → "custom-kind" ✓
engine.Types.Mime(".custom")    // → "application/custom" ✓

// But these DON'T work (_allKinds and _mimeToKind not updated):
engine.Types.KindOf("custom-kind")           // → null ✗ (should be "custom-kind")
engine.Types.KindOf("application/custom")    // → null ✗ (should be "custom-kind")

// Which means Type.Kind fails for custom types:
var data = new Data("x", bytes, Type.FromMime("application/custom"));
data.Context = context;
data.Type.Kind    // → null ✗ (calls KindOf internally)
```
