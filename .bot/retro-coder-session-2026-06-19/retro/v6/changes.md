# Changes Applied — Retro v6

**Target:** `characters/coder/memory/MEMORY.md`

All changes backed by session findings.

---

## SC13 — Verb+Noun flashing sign strengthened to fire at design/proposal time

**Backing:** F2 — Verb+Noun violations every session (5/5 sessions: BornRow, ValueClr, NewEmpty, LoadFromFileAsync, ParseNextSegment). Rule was already in MEMORY.md but only as a write-time tripwire. Coder kept proposing Verb+Noun names in plans before writing.

**Change:** Added "FIRES AT DESIGN TIME, NOT JUST WRITE TIME" header + concrete examples from these sessions + instruction to scan intended names before opening editor.

---

## SC12 — Root-cause-first as a flashing sign

**Backing:** F1 — hack-first pattern in 3 sessions. "don't just hack through fixing this, I dont trust you." Current link to `feedback_root_cause_not_obvious_place.md` was buried in Small Rules and too quiet.

**Change:** Added `🚩 ROOT CAUSE BEFORE CODE` flashing sign block between the CLR rule and the workflow section. Requires stating WHY in one sentence before any edit.

---

## SC14 — Design discussion before significant C# changes

**Backing:** F3 — "dont change c# lets talk about this before you do that" (1f86445f, 7704f9f0). Coder implements before design is agreed.

**Change:** Added to Coder discipline section: design verbally first, wait for OK, then implement.

---

## SC18 — Operations on owning type

**Backing:** F7 — datetime.Ticks in item.cs, Number.FromText named as Text.FromText, prPath parsing in goal/list (d760f3f1, multiple). OBP rule stated in CLAUDE.md but not as an actionable check in MEMORY.md.

**Change:** Added to Coder discipline section: ask "which type owns this?" before placing logic. Concrete examples from sessions.

---

## SC17 — Lead with code, not prose

**Backing:** F6 — "I need code, I need flow, text says nothing to me, didn't read what you wrote, to much" (d760f3f1).

**Change:** Added to Communication style section, before the existing "high-level first" rule.

---

## SC15 — Rebuild = earn new information first

**Backing:** F4 — "if you are building again, why dont you add debug message before building again?" (d760f3f1). Rebuild cycle without learning.

**Change:** Added to Small Rules.

---

## SC16 — Fix at the correct layer

**Backing:** F5 — "stop looking at c# code, fix the pr file" (d760f3f1). Wrong diagnostic layer.

**Change:** Added to Small Rules.

---

## SC19 — Coincidental duplication

**Backing:** WD-a — bot argued for shared logic extraction; when design fixed, duplication vanished (6b1d7425).

**Change:** Added to Small Rules.

---

## SC20 — .pr files are build output, not hand-editable

**Backing:** SC-c — "only files you are allowed to hand edit is the plang builder" (7704f9f0). Strengthens existing `feedback_plang_build_file_scoping.md`.

**Change:** Added to Small Rules with explicit statement: only `system/builder/*.goal` files are hand-editable.
