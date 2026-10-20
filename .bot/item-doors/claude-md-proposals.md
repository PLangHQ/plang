## architect — v1 — 2026-10-18
**Target:** `CLAUDE.md`, the "OBP Shape Smells" list (Shape), beside "glued name"
**Why:** Ingi, to the coder on item-doors: "you fail often on naming variables what they are, 'declared' for example, just name it what it is, if it´s a type, name it type, dont be creative", and to me: "this really is important, not to be creative with variable names". In the same session: `_held` (an action), `held` (a value), `declared` (a type), `_hasWrapped` (a list's state). Every bot reads CLAUDE.md; the rule isn't there yet.
**Proposed change:** add to the Shape smells:

```
- **creative name** — a variable, field or parameter named for its role or its origin instead of what it holds: `declared` for a type, `held` for an action, `wrapped`, `fresh`, `incoming`. Name it what it is: a type is `type`, an action is `action`, a value is `value`. Two of a kind in one scope are told apart by what they are (`type` and its `kind`), never by an invented adjective. Ingi: "dont be creative … this really is important".
```

## architect — item-doors 107/119/124 — 2026-10-20
**Target:** `CLAUDE.md` (Runtime2 Conventions, beside "A value writes what it is; the writer decides how it looks")
**Why:** Stage 2 made `Output` the only way to write and gave every output kind its pairs (what a value looks like in that kind). A value that writes a member by calling `member.Output(writer, …)` directly skips the member's pair. The coder's first pass missed about 20 such sites, found only by a later grep (a89b3f12f). Future code needs the rule where it looks.
**Proposed change:**

```
- **One door for writing.** A value writes itself in `Output` (there is no sync `Write`), and writes a member through `writer.Kind.pair.Output(member, writer, mode, context)`, never `member.Output(writer, …)` directly, or the member's pair in that output kind is skipped. A pair (`type/format/IOutput`) lives in its input type's folder at `format/<output kind>`, found by the value's class, then its bases. A value never asks what its writer is (no `writer is X`, no `writer.Kind` read inside a value); the writer decides how a kind looks (`Bytes(byte[], kind)`). A Data writes itself, and its value goes through the door inside.
```
