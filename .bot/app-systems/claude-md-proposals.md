## architect — app-systems — 2026-09-26
**Target:** /CLAUDE.md (PLang Syntax)
**Why:** Ingi: "comment above says what next code line does". The parser follows it (lines above a goal's name are the goal's comment, lines above a step are that step's), but 356 of 594 `.goal` files in `os/` and `Tests/` describe the goal under its name, so the description lands on step 0. Bots writing `.goal` files copy that shape.
**Proposed change:**
```
- A comment line (`/ …`) describes the NEXT code line. A goal's description goes above its name; a step's comment goes above the step. Never put a goal's description under its name: it would become the first step's comment.
- A folder's docs in a plang app are `start.md` (as `Start.goal` is the entry), not `readme.md`.
```

## architect — app-systems — 2026-09-26
**Target:** /CLAUDE.md (Runtime2 Conventions, the `app/` lowercase bullet)
**Why:** Ingi ruled plang vocabulary lowercase in C# members too, so the plang path, the C# path and the file path match letter for letter ("it started lowercase with modules … it looked good to have everything in lowercase"). The current text says the opposite.
**Proposed change:** replace "**Property names on `app.@this` stay PascalCase** (`.Cache`, `.Builder`, `.Code`, `.Module`, `.FileSystem`, `.Goal`, etc.) — only the *types* live in lowercase singular namespaces. So `ctx.App.FileSystem.Read(...)` is property access (stays capital); `app.filesystem.@this` is the type." with:
```
**plang vocabulary is lowercase in members too.** Everything plang can reach is a lowercase member (`app.type`, `app.goal`, `app.variable`, their `list`, `current`, `all`, `on`, and the facts a face shows), so `%!app.type["text"]%` ↔ `app.type["text"]` ↔ `app/type/type/this.cs`. C# plumbing plang never navigates stays PascalCase; a C# keyword keeps its `@` (`app.@event`); an item's own `Type` (its type entity) stays.
```
