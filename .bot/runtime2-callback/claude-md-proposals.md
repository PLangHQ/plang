# CLAUDE.md proposals — runtime2-callback

## architect — v2 — 2026-05-05
**Target:** `characters/architect/character.md` (new section near the end, before "When You're Done"). Fallback target: `/CLAUDE.md` (new top-level section) if the character file route isn't preferred.

**Why:** During this branch we built a small browser-based review tool at `.bot/architect/server.py` that lets the user view architect output (`v<N>/plan.md`, `summary.md`, etc.) rendered as HTML and leave **per-line comments** that persist in `.bot/<branch>/architect/comments.json`. Comments now carry `author`, `status` (`open` / `resolved` / `disagreed`), and `parent_id` (for threaded replies). When the user clicks "📨 Send to Architect" the server stages a marker file and copies a prompt to the clipboard; the user pastes it into Claude.

Without this in the architect's instructions, future architect sessions will read `comments.json` as raw data and silently edit the file (or ignore the schema), instead of using the HTTP API the way the user can see in the UI. They'll also miss the start/restart commands and the resolve-vs-disagree workflow that the user just designed. This is canonical workflow on the architect role going forward, not branch-specific work.

**Proposed change:**

```markdown
## Reviewing User Comments (review server)

The user reviews your output through a tiny stdlib Python web app at
`.bot/architect/server.py` (port **8081**). It serves the current branch's
`.bot/<branch>/architect/*.md` files rendered as HTML and lets the user leave
per-line comments. Comments live in `.bot/<branch>/architect/comments.json`
and are committed alongside your work.

### Starting / restarting the server

```bash
# start (foreground)
python3 .bot/architect/server.py

# restart cleanly (it grabs port 8081, so any old instance must die first)
PID=$(pgrep -f "architect/server.py"); kill -9 $PID 2>/dev/null
setsid python3 .bot/architect/server.py > /tmp/srv.log 2>&1 < /dev/null &
disown
```

The server auto-detects the current git branch and serves
`.bot/<branch>/architect/`. Switching branches and refreshing the browser
is enough — no restart needed.

### Comment schema

```json
{
  "id": "10-char hex",
  "line": 7,                    // 1-based source line in the .md file
  "text": "the comment",
  "author": "user" | "architect",
  "status": "open" | "resolved" | "disagreed",
  "parent_id": null | "<id>",   // reply threading
  "ts": "2026-05-05T10:30:00"
}
```

### Workflow when the user pastes "read my comments at .bot/<branch>/architect/comments.json and address them"

1. Read `comments.json` and identify all `status: "open"` entries authored by
   `user` (skip already-resolved ones; skip your own architect entries).
2. For each, decide:
   - **Agree → resolve.** Make the change in the relevant file(s), then mark
     the user comment resolved via the API:
     ```bash
     curl -s -X PATCH "http://localhost:8081/api/comment?id=<USER_COMMENT_ID>" \
       -H 'Content-Type: application/json' -d '{"status":"resolved"}'
     ```
     A short confirming reply is helpful but not required when the change
     itself is the answer.
   - **Disagree → reply with reasoning, then mark disagreed.** Post your
     reply with `parent_id` set to the user's comment id, then PATCH the
     user's comment to `disagreed`:
     ```bash
     curl -s -X POST http://localhost:8081/api/comment \
       -H 'Content-Type: application/json' \
       -d '{"file":"<file>","line":<n>,"text":"<reasoning>",
            "author":"architect","parent_id":"<USER_COMMENT_ID>"}'

     curl -s -X PATCH "http://localhost:8081/api/comment?id=<USER_COMMENT_ID>" \
       -H 'Content-Type: application/json' -d '{"status":"disagreed"}'
     ```
   - **Need clarification → reply with `parent_id`, leave status `open`.**
     Same POST as above; do not PATCH the user's comment.

3. Always go through the HTTP API. Do **not** edit `comments.json` directly —
   the server normalizes timestamps, IDs, and ordering, and direct edits
   bypass that.

4. The orange gutter dot in the UI clears only when no `open` comments
   remain on that line. Resolved/disagreed comments stay visible (struck
   through or red-bordered) for audit.

5. If the user has not visited the page since you posted replies, push them
   a short `PushNotification` summarizing what you did — they may be on
   their phone and the browser tab isn't refreshed.
```

---

*Footer: this proposal documents a tool the architect built for the user
during this branch. No incident triggered it; it's a new capability that
needs persistent instructions so it isn't reinvented next session.*


## coder — v4 — 2026-05-05
**Target:** `characters/coder/character.md` — new sub-section under "Testing Requirements" (near the existing "Both C# and PLang tests are required" line). Fallback target: `/CLAUDE.md` if the character file route isn't preferred.

**Why:** On `runtime2-callback` Stage 4 I closed the branch with all 2720 C# tests green but left 11 `Tests/Callback/*/Start.test.goal` stubs that test-designer wrote as `- throw "not implemented"` showing as **stale** in `plang --test`. Some stubs (AskWithVars, AskVarsResumeBindsValue) genuinely need builder support that's out of branch scope per Ingi's Q2 — but most (RunCallbackVerb, ErrorCallbackSurface, RunNonCallback, TamperedSignature, CallbackTimeoutSetting, ErrorCallbackOutsideHandler, DurabilityRoundTrip, InProcessResume) only needed me to write the goal text and run `plang build` + `plang --test`. I have that capability — the character file already says "Building plang files works" and "Run plang tests with `plang --test`" — but nothing told me that **handing back stale .test.goal stubs counts as incomplete stage closure**. I treated stubs the test-designer wrote as a contract I owed only on the C# side. Ingi flagged this in conversation: "you have plang build support, read the doc and propose claude change so you know that you should write and build plang tests."

The fix is one explicit rule: when test-designer leaves `.test.goal` stubs, I write them out, build with `plang build`, run `plang --test`, and call out which (if any) genuinely cannot be written because they depend on out-of-scope work — naming the missing piece. "Stale" is a real failure mode that the user shouldn't have to chase.

**Proposed change:** Add this section to `characters/coder/character.md` directly after the existing "Both C# and PLang tests are required" line in the "Testing Requirements" section.

```markdown
### Test-designer's `.test.goal` stubs are part of your contract

When test-designer writes `Tests/<Area>/<Scenario>/Start.test.goal` stubs with
`- throw "not implemented"` bodies, they are owed work — same as the C#
`Assert.Fail("Not implemented")` stubs. `plang --test` reports them as **stale**;
that is a failure mode, not a green light. Stage closure means:

1. Write the goal body that exercises the C# code you just landed. Use the
   stub's spec comment as the contract.
2. Build the .goal: `plang build` (or
   `plang '--build={"files":"<scenario>.goal"}'` for one file). The builder is
   non-deterministic — read the resulting `.pr` after every build and verify
   module/action/parameters match the step text.
3. Run `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`. The
   `Tests/<Area>/*` you wrote should now pass; **stale count for your branch's
   scenarios drops to zero**.
4. If a stub genuinely cannot be written because it depends on work that's
   out of branch scope (e.g. a builder annotation, a module action that doesn't
   exist on this branch), say so explicitly in your stage summary — naming the
   missing piece, not just "stale". Don't leave the user to figure out which
   stale entries are deliberate vs. forgotten.

A green C# suite with red/stale `.test.goal` stubs from your stage is **not**
done. Both layers are the deliverable.
```

*Footer: this proposal is filed against an incident on this branch — Stage 4
shipped with 11 stale .test.goal stubs that I should have written and built.
Ingi flagged it explicitly. Falls under the architect/test-designer/coder
proposal exception, not the reviewer-bot blanket prohibition.*
