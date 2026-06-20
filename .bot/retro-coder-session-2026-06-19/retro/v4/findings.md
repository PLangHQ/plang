# v4 Findings — Architect sessions 2026-06-16 and 2026-06-18

## Sessions analyzed

| File | Date | Lines |
|------|------|-------|
| b07eaf59 | 2026-06-18 | 917 |
| b781b066 | 2026-06-18 | 182 |
| 166443d4 | 2026-06-16 | 212 |
| 6aa7c395 | 2026-06-16 | 44 |

## Kept (new — not in architect MEMORY.md)

### A1 — Verb+Noun violations in own output
- **Category:** frustration + self-correction
- **Session:** b07eaf59
- **Quote:** "Two good corrections — actor.Channel.ChannelNames (and my Identify/LifecycleFor suggestions) violate the verb+noun rule I should've caught."
- **Trigger:** Ingi: "verb+noun (or noun+verb) name on public properties/method, e.g. actor.Channel.ChannelNames, that is not allowed." Architect had just proposed these names.
- **Lesson:** The Verb+Noun flashing sign applies to the architect's own naming suggestions, not just code under review. Added as prominent callout to MEMORY.md.

### A2 — Rubber-stamping during OBP audit
- **Category:** frustration
- **Session:** b07eaf59
- **Quote:** "You're right, and I rubber-stamped instead of applying my own rule. Let me reason it out and check."
- **Trigger:** Ingi: "ErrorCategory — didn't you spot this? where is your logic." Architect had skipped checking a known rule when reviewing code.
- **Lesson:** Knowing a rule ≠ checking it. During an OBP audit, actively apply each rule to every member. Added to Small Rules.

### A3 — False capability claim about Fluid
- **Category:** self-correction + wrong-doc
- **Session:** b07eaf59
- **Quote:** "You're right, and I was wrong — Fluid is async-aware: an async member accessor returns ValueTask and the await runs lazily at access time."
- **Trigger:** Ingi showed evidence that Fluid supports async natively; architect had stated "can't have async members in Fluid."
- **Lesson:** Don't assert library capability limits without verifying. Check the docs. Added to Small Rules.

## Dropped

- b07eaf59 "hand-rolling launcher instead of using documented scripts" → Already in architect MEMORY.md (reference_review_server.md, reference_branches_server.md) and is the canonical retro example.
- b07eaf59 decomposition rule violated right after citing it → SC7 already captured; this is an architect instance of the same pattern.
- b781b066 optimizing for wrong metric (wire weight vs security) → Too domain-specific for a general rule. One-off design review lesson.
- 166443d4 clean session, no findings.
- 6aa7c395 too short, no findings.
