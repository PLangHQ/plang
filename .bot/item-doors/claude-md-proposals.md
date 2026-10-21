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

## architect — item-doors 167–202 — 2026-10-20
**Target:** `CLAUDE.md` (Runtime2 Conventions); **WITHDRAWS** the entry above ("One door for writing", item-doors 107/119/124)
**Why:** Both audits found the pairs and their door were the wrong shape: a lookup beside `Output` at ~100 sites, held by a build-time lint (PLNG006), keyed by C# class (so `kind` needed an undo pair). Ingi redesigned it with the architect (decisions 167–188): a value always writes itself, by double dispatch; a kind never writes; the writer is made with its view and its asker. The door, the pairs and PLNG006 are gone, so the entry above would teach a shape that no longer exists.
**Proposed change:** replace "A value writes what it is; the writer decides how it looks" with:

```
- **A value always writes itself.** Every write is `value.Output(writer)`: `item.Output(writer) => writer.Write(this)`, non-virtual, and each writer hands the value itself, typed, so C# picks the value's overload for that writer (`Write(IWriter)`, its own write; `Write(text.Writer)` and `Write(formal.Writer)` only where a value looks different, e.g. a type in text is its word `list<path>`, an action in formal is its call). The code for a look lives in the value's own class. A writer is made with its view (`View.Store`, `View.Out`, `View.Debug`, constants) and its asker (`writer.Context`, never null); no value reads the view: the view shows its tagged members and decides whether a value that holds variables is kept as written (Store), used (Out), or both (Debug: `as written → value (type)` or `not set`). **A kind never writes**: a kind whose difference is how its value is written (duration's `short`/`iso`, size's `iec`/`si`) is a subclass of its type that writes and parses itself; any other kind (a number's precision, a file format) stays a kind and writes nothing. A C# object that isn't a plang value is written by the value's own walk over its `Host`'s tagged members.
```

## architect — item-doors 202 — 2026-10-20
**Target:** `CLAUDE.md` (Runtime2 Conventions, the "`app/` is lowercase + singular" bullet)
**Why:** The bullet says "Everything plang navigates is a lowercase member", but many facts plang reads are PascalCase in C# (`Name`, `Count`, `Created`, `Message`, `Id`, `Url`, `IsCached`), and plang reads members ignoring case (`item/this.cs:272`). Ingi narrowed the rule to what it guards rather than sweep hundreds of C# lines: "b is fine, it´s not to important".
**Proposed change:** replace "**plang vocabulary is lowercase in members too.** Everything plang navigates is a lowercase member (…)" with:

```
**plang's nodes are lowercase.** A concept that maps to a folder is a lowercase member (`app.type`, `app.goal`, `app.variable`, `app.module`, `app.data`, `app.actor`, their `list`, `current`, `all`, `on`), so `%!app.type["text"]%` ↔ `app.type["text"]` ↔ `app/type/item/text/this.cs`. A value's facts may stay PascalCase in C# (`Name`, `Count`, `Created`): plang reads members ignoring case, and a .goal writes them lowercase.
```

## coder — item-doors 214/215/218 — 2026-10-21
**Target:** CLAUDE.md (Debugging)
**Why:** A command-line flag is now a setting's path (214/215): `--debug={…}` is refused and names its replacement, `--debug.setting.…`. A setting flag alone switches its mode on (218). The two Debugging examples show the removed form.
**Proposed change:** replace the two `--debug={…}` lines under "## Debugging" with:

```
- `plang --debug.setting.goal=Start` — debug specific goal (a debug setting alone switches debug on)
- `plang '--debug.setting={"goal":"Start","step":3}'` — debug specific step (several options at once: a dict at the setting's path)
- Any setting from the command line: `--<path>=<value>`, `app.` optional — `--channel.setting.format=plang`, `--build.setting.cache=skip`, `--test.setting.parallel=1`. A path no setting takes is refused.
```
