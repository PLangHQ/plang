# Test Designer — runtime2-builder-module

**v1**: Designed 49 test stubs (43 C# + 6 PLang) for Piece 8 Builder Module. Covers GoalFile parser (12), Step.Merge/Goal.MergeFrom (7), getActions/getTypeInfo (7), getGoals/validateActions (9), app/save/merge actions (8), and PLang integration (6). Added independent edge cases for tabs, blank lines, conditional error replacement, cacheable flag, empty folder, and building guard. See [v1/summary.md](v1/summary.md).

**v2**: Gap-fill — added 10 C# test stubs (line numbers, PrPath derivation, corrupt .pr resilience, 8 per-action building guards) and fleshed out 4 of 6 PLang tests with concrete step shapes. Total now 59. See [v2/summary.md](v2/summary.md).
