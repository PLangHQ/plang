## architect — event-action — 2026-10-29 (3)
**Target:** characters/architect/character.md ("How You Work — MANDATORY")
**Why:** in one branch the architect kept proposing eager placement (prepared at start, bind or birth) and several members for one question, and let a bot's "it's sync, so it can't load" through. Ingi: "you continuesly fail to follow obp and the lazy loading … You need to harness this somehow, it needs to be shared with other bots. The way to do that is through obp documentation." The patterns are now `obp-patterns.md` 11 and 12 (branch `docs/obp-lazy`).
**Proposed change:**
```
- **Two checks before any proposal or ruling goes out, yours or a bot's** (`Documentation/v0.2/obp-patterns.md` 11, 12): who asks for each new piece, and when? It must be "its first asker loads it", never "it's prepared at start, bind or birth", with no exception for a sync or early caller. Who answers each question? The element the question is about, through one member.
```

## architect — event-action — 2026-10-29 (2)
**Target:** characters/architect/character.md ("Writing Plans", before "Preparing test-designer")
**Why:** the event-action plan went to the coder holding decisions that collided (Ingi's pull rule and the architect's push rule, plus later reversals); the coder built the wrong one. Ingi: "I think it is very important step before we send the plan to next bot (often coder), to read over the those decisions and see if they colide. This should be the last item to check before sending the plan."
**Proposed change:**
```
### The last check before sending a plan

Before a plan (or a change to it) goes to the next bot, read every decision in the branch's log and check whether any collide. Write the replaced ones in a "Replaced decisions" table at the top of the log (what was replaced, by which decision, what changed), and make `start.md` say only what holds now. Only then send.
```

## architect — event-action — 2026-10-29
**Target:** /CLAUDE.md (Runtime2 Conventions)
**Why:** Ingi, reading `goal/this.cs:405–414` (`if (result.Returned) { … } return result;`): "we should always try to return early, so `if (!result.Returned) return result;` … (this is also for code analyzer, so send him)". Not in CLAUDE.md or the conventions today.
**Proposed change:**
```
- **Return early.** A guard that ends a method returns first; the rest of the body is never nested inside an `if`. Write `if (!result.Returned) return result;` then the work, never `if (result.Returned) { … } return result;`. The codeanalyzer flags the nested form in every file a branch touches.
```
