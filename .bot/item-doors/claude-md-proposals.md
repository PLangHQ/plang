## architect — v1 — 2026-10-18
**Target:** `CLAUDE.md`, the "OBP Shape Smells" list (Shape), beside "glued name"
**Why:** Ingi, to the coder on item-doors: "you fail often on naming variables what they are, 'declared' for example, just name it what it is, if it´s a type, name it type, dont be creative", and to me: "this really is important, not to be creative with variable names". In the same session: `_held` (an action), `held` (a value), `declared` (a type), `_hasWrapped` (a list's state). Every bot reads CLAUDE.md; the rule isn't there yet.
**Proposed change:** add to the Shape smells:

```
- **creative name** — a variable, field or parameter named for its role or its origin instead of what it holds: `declared` for a type, `held` for an action, `wrapped`, `fresh`, `incoming`. Name it what it is: a type is `type`, an action is `action`, a value is `value`. Two of a kind in one scope are told apart by what they are (`type` and its `kind`), never by an invented adjective. Ingi: "dont be creative … this really is important".
```
