# Auditor v5 Summary — Re-review of Coder v5 Fixes

## What this is

Re-review of coder v5 fixes for the three auditor v4 findings on the Setup.goal system.

## What was done

Verified all three findings are correctly addressed:

### F1: IsSetup filter — FIXED
Four return paths now filtered:
- `GetAsync` relative path (line 120): `if (loaded is { IsSetup: true }) return null;`
- `GetAsync` root path (line 138): `if (result is { IsSetup: true }) return null;`
- `GetByPrPathAsync` cache (line 222): `return cached.IsSetup ? null : cached;`
- `GetByPrPathAsync` disk (line 237): `if (loaded is { IsSetup: true }) return null;`

Bonus: The coder found a real bug in the original `GetByPrPathAsync` cache path — the old `&& !cached.IsSetup` condition caused a fallthrough to disk load (NPE). The new ternary returns null immediately for cached setup goals.

5 new tests in GoalsTests.cs cover all paths including disk-loaded .pr files with IsSetup=true. Positive control test confirms non-setup goals still load correctly.

### F2: Goal loading before Setup.RunAsync — FIXED
`LoadFromDirectoryAsync` at Executor.cs line 368 now runs before `Setup.RunAsync` at line 371. Scans `engine.AbsolutePath` recursively for `*.pr` files. All goals (including setup) are in the collection before setup iterates them.

### F3: Conditional setup interception — FIXED
Line 375: `goalName.Equals("setup", ...) && engine.Goals.Setup.Goals.Any()` — only short-circuits when setup goals exist. A user goal named "Setup" without any actual setup goals will now run normally.

## Verification

- All 1490 C# tests pass (1485 + 5 new)
- Code read in full — no new issues introduced
- Contract is now consistent: `Get()`, `GetAsync()`, `GetByPrPathAsync()` all filter IsSetup

## Files reviewed
- `PLang/App/Engine/Goals/this.cs` — IsSetup filters verified at all 4 return points
- `PLang/Executor.cs` — LoadFromDirectoryAsync ordering, conditional setup check
- `PLang.Tests/App/Core/GoalsTests.cs` — 5 new tests read and verified
