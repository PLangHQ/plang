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
