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
