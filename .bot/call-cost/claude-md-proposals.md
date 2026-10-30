## architect — call-cost — 2026-10-30
**Target:** /PLang.Tests/CLAUDE.md (or /CLAUDE.md's "Speed record", whichever holds test conventions)
**Why:** call-cost pinned its savings as C# byte tests. Pins set at "measured + 2–4%" went red in the full sweep while green alone (40,656 against < 40,000), because bytes move with process order. The auditor's A1; fixed by setting each bound between the pin's two states.
**Proposed change:**
```
- **A byte pin guards its claim.** A C# test that pins allocated bytes sets its bound between the two states it tells apart: the bytes with the change and without it (a step cache not made at birth: 38,912 vs 86,036 → < 60,000). Never "measured plus a few percent": bytes move with process order and with what else the sweep ran. Drift inside a band is the bench's (`Tools/Bench/record.md`), not a test's. A pin that guards a small saving inside a whole run measures the saving at the ask itself.
```
