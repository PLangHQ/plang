# Hard check — would the stage-1 (full-match) cut hit the builder's own prompts?

Coder: tester (plang-f2). The cut applies only to full-match `%x%` message content.

## How the builder's llm messages are made

| caller | content | full-match? | rides the cut? |
|---|---|---|---|
| Properties.goal:14 | system `"%propertiesSystemMsg%"`, user `"%propertiesUserMsg%"` | **yes, both** | **YES** |
| Start.goal:80 (SourceFix) | system `"%sourceFixSystem%"` | yes | yes, but small (~1,134) |
| Start.goal:80 (SourceFix) | user `"%!error.Key%: … %goal%"` | no (partial) | no (waits for the door) |
| Start.goal:47 (fix) | user `"Steps %…% were refused …"` | no (partial) | no |
| Decide.goal:20,26 | `llm.decider` State/Question | — | **NO** — the decider sends its own body dict via http.request (TypeSafe.cs:33-52); State never touches the message-content writer. Prompt C (~26.8k) is safe. |

So the one real risk is **Properties**, whose system AND user content are full-match `%x%`.

## The longest prompt today (real rendered, from pick_golden.json `.user`)

Properties **user** message (`%propertiesUserMsg%`), across the 9 golden goals:

| goal | chars | | goal | chars |
|---|---|---|---|---|
| Start | **19,899** | | Decide | 9,884 |
| WeeklyReport | 19,060 | | Guard | 7,212 |
| Checkout | 15,058 | | Compare | 5,668 |
| Show | 11,449 | | Page | 5,645 |
| Build | 10,785 | | | |

Properties **system** (`%propertiesSystemMsg%`): ~8,345, roughly fixed (the kernel).

The user message is **goal-dependent**: it injects per-action notes for every listed action
(notes corpus totals 45,108 chars across all actions), plus the goal's steps, types, settings.
Its ceiling grows with goal size and distinct-action count toward ~45k+.

## Verdict

**A 20,000-char default is unsafe.** The longest builder prompt today (Start, 19,899) clears 20k
by **81 characters**, and these prompts grow with the goal. Any larger goal silently truncates its
Properties user message → the writer sees a cut prompt → wrong build output. A fixed default that
sits safely above the ceiling would need to be ~60k+, which largely defeats the user-protection
purpose (a 50k runaway value would pass).

## Recommendation

**The builder opts out in its own step** — `set %!llm.setting.length% = <large>` before the
Properties `llm.query` (plang's own prompt, authored to go whole, grows unbounded with goal size).
That matches the plan's "a developer raises them where needed," and the builder is the archetypal
such developer. Keep the **user-facing default modest** so the cut actually catches runaway values —
Ingi sets the number; I'd suggest 20,000 for users now that the builder no longer depends on it.

Nuance: `%!llm.setting.length%` is a module setting, so setting it in a builder step raises it for
that actor's later llm calls too. Acceptable (the builder wants no cut on any of its own prompts),
but if a per-query cap is wanted, that's a follow-up.
