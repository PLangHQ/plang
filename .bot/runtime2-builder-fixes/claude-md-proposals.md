## claude-code — v1 — 2026-05-22
**Target:** /CLAUDE.md
**Why:** The "PLang Syntax" foreach example in CLAUDE.md is inverted relative to the actual runtime convention. It reads:

> `foreach always calls a goal, does not support sub steps. Syntax: `foreach %products%, call DoProduct item=%product%`, `item=%variableName%` not `%variableName%=%item%``

But the actual convention (Ingi confirmed on this branch after I'd built the wrong shape into the builder source): `%item%` is the foreach's auto-bound iteration variable (fixed name — `loop.foreach`'s `ItemName` defaults to "item"). `call X p=%item%` reads as "create parameter `p` on the called goal X, load it from `%item%`". The CLAUDE.md note has it backwards — following it produces .pr files where the foreach's iteration value never reaches the called goal (the value side becomes a literal `%var%` string because the named var isn't in scope), which surfaced on this branch as cascading NREs in the builder.
**Proposed change:**
```
- foreach always calls a goal, does not support sub steps. Syntax: `foreach %products%, call DoProduct product=%item%`. `%item%` is the foreach's auto-bound iteration variable (fixed name). The left side `product=` names the parameter the *called goal* receives; the right side `%item%` is the value being passed in. Do NOT write `item=%product%` — that would create a parameter named "item" with value `%product%`, but `%product%` isn't in the caller's scope at that point.
```
