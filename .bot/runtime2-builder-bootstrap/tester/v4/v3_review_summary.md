# tester v3 — review summary (carried forward to v4)

My v3 verdict was `needs-fixes`. The fixes I asked for were:

**V3-1: `IsCatalogDescription` had zero C# unit tests.**
- Helper at `DefaultBuilderProvider.cs:653–664` recognizes 4 catalog-description shapes (`"X"`, `"X?"`, `"X = default"`, `"%var% X"`) anchored on `typeName`.
- Coverage report showed lines 659–663 (the match-true path) were never executed by any C# test — every C# call fell through the `!v.StartsWith(typeName) → return false` early-out.
- `BuilderValidateValid.test.goal` was the only signal exercising the positive match. A regression flipping the helper to always-return-false would leave the C# suite green.

**V3-2: `math.<op>.ExamplesForLlm()` rendered output had no C# assertion.**
- `ExampleRenderer.Render()` was at 85.2% line coverage but no test asserted the rendered chain shape.
- `Loop.test.goal` was the only signal. A renderer regression would surface only when someone happened to rebuild a goal matching the RHS-arithmetic pattern.

**Carryover from earlier rounds (out of v3 scope, still tracked):**
- F4: 23 PLang reds across Signing (9), Identity (2), UI (2), Event (3), Goal-call (1), ContextVars (1), Crypto (1), Test/Discover (1), ErrorTypes (1), App/SetupGoal (1), ConditionCompound (1), ForeachDictionary (1) + ForeachCallsGoalPerItem.
- F5: locale-format — no non-Invariant culture test.
- F6: `promoteGroups` 0% coverage, no goal references it.
- F8: cosmetic test mislabel.

I wrote `needs-fixes` with the recommendation to bump to `approved-with-followups` if F4 was genuinely separate-branch work. The coder's response (`6fd35065`) addresses V3-1 and V3-2; F4 remains untouched, presumably still scoped out.
