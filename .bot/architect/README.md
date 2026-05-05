# Architect Review Server

Tiny Python web app for reviewing the architect's output of the **current branch**
and leaving per-line comments that travel with the work.

## Start

```bash
python3 .bot/architect/server.py
```

Then open <http://localhost:8081>.

No dependencies — Python 3 stdlib only. Port: **8081**.

## What it shows

- Auto-detects the current git branch (`git branch --show-current`).
- Lists every `*.md` file under `.bot/<branch>/architect/` (recursive).
  - Root `summary.md` first, then `v1/`, `v2/`, ... in numeric order.
  - Each file shows a badge with its comment count.

## Commenting

- Click any **line number** to open a composer below that line.
- Type the comment, hit **Save**.
- Lines with comments turn orange. Click ✕ to delete.

## Where comments live

```
.bot/<branch>/architect/comments.json
```

Per-branch, committed alongside the architect's output. Format:

```json
{
  "v2/plan.md": [
    { "id": "ab12cd34ef", "line": 42, "text": "...", "ts": "2026-05-05T14:23:01" }
  ]
}
```

The architect (Claude) reads this file directly when you ask "see my comments".
