# Mime → binary/kind, lazy narrowing (compare-redesign)

**Decided with Ingi, 2026-06-16.** Supersedes the §C "where does the declared
label ride" question — the answer is "it doesn't; undecoded content is `binary`."

## The model (one rule)

**Provenance decides.**
- **Bytes off I/O** (file / http / stream) → always `binary` + kind, **lazy**.
  `.Type` (sync) reports `binary`; the kind says *how to read*. On value access
  (`Value()` via the `.` navigation door) the kind's reader decodes and the
  holding `Data` rebinds to the real type (`dict`/`list`/`image`/`table`/`text`).
- **A value already in memory** keeps its own type; `as <kind>` only refines the
  kind (`%blog.body% as md` → `text/md`, no binary stage).

`item` / `clr` never come out of mime-typing. `clr` is host-objects-only.

`%x!type%` → Data's own `Type` property (sync) → `binary`. `%x.foo%` → `.`
navigates into the value → `Value()` parses → narrowed type.

octet-stream / unknown mime → `binary`, kind **null** (nothing to decode).

## Reader lookup: kind → inner type → that type's reader

`Readers.Of` is type-primary (`(type,kind)` → `(type,*)`), no `(*,kind)`. A
`binary/json` holder misses. The fallback: **the kind names the inner type**, then
look up that type's reader.
- `binary/json` → kind `json` → type `item` → `(item, json)` → dict/list
- `binary/jpg`  → kind `jpg`  → type `image` → `(image, *)` (wildcard) → image
- `binary/csv`  → kind `csv`  → type `table` → `(table, csv)` → table
- `binary/md`   → kind `md`   → type `text`  → `(text, *)` → text

## Edits (in order)

1. **`Format.TypeFromMime`** → uniform `binary` + canonicalised-subtype kind.
   Drop the media / tabular / item branches. octet-stream → `binary` null kind.
2. **Reader fallback** → kind → inner type → retry the lookup. (Owner + name TBD
   with Ingi — see open question.)
3. **Drop the `(object, json)` reader** (json → `item`). Full `object`-type
   removal is a separate ~37-site pass.
4. **`file read.cs:69-79`** → delete the eager-image special case (images lazy now).
5. **`channel.StampType:313`** → drop the `text/plain → text` shortcut (text off
   I/O is `binary` too).
6. **`clr` §C** → remove the `Judge` label arms + `clr._declared`/`Labeled`.
7. **Tests** → rewrite the 7 assertions to `binary/<kind>`; the strict
   `as image/gif` ones stay `image` (set validates+elevates). Redirect/delete the
   test-only `Type.FromMime`.

## STATUS (landed + pushed through d1ff6022d)

All flip work committed and pushed. Every flip-caused test failure resolved EXCEPT one
(below). Suites at/below the env-flaky baseline: Types 11≤31, Data 16≤17, Modules 46≤49,
Wire 16 (baseline 15, the +1 is the open one).

Done: kind value class; TypeFromMime→binary/kind; StampValue always bytes; json/csv/
text/goal readers decode bytes via the text ctor; kind.Type fallback; table→item
(+per-column-format TODO in table/this.cs); EnumerateItems moved onto the types;
text/plain→txt narrow + deterministic CanonicaliseKind tie-break; .pr→goal narrow;
~26 edit-7 test rewrites.

### OPEN #1 — ThrowTimeSnapshot_EditSurvivesResume (the one remaining, deterministic)
Throw-time snapshot resume fails: `CallbackGoalNotFound … ''`. On Restore, the callback
frame's `goalName`/`goalPrPath` read back EMPTY. Traced:
- Capture is correct — `SnapshotChain()` is no-copy (stable refs), the call is live at
  capture, so `call.Capture` writes `goalName="G"` (callstack/call/this.Snapshot.cs:29).
- `Io.Get<string>("goalName")` reads straight off the parsed JsonNode (snapshot/Io.cs:40)
  — not via the json reader, so my json change isn't it; correct in isolation.
- The serializer `Render` (snapshot/serializer/Default.cs) DOES emit each frame's entries.
- So the drop is somewhere in the dict-tree→wire→parse round-trip of the nested frames.
  NEXT STEP: dump `app.SnapshotToWire(app.Snapshot(err))` json and diff — is `goalName`
  present in the wire string (→ read-side bug) or absent (→ serialize-side bug)? That one
  observation localizes it. Suspect interaction with a flip change to how a string-valued
  Data / dict entry / the frames List<snapshot.@this> serializes. Not yet whether it broke
  at edit-2 (source.Value fallback) or the flip — bisect by checking the test at 3d20ba803.

### OPEN #2 (semantics, deferred to Ingi) — invalid-json on access
A body declared json that fails to parse: should `Value()` ERROR (strict — kind is a
claim) or fall back to raw text (lenient — old behavior)? Get_InvalidJson was rewritten
to only assert the raw is recoverable (sidesteps it); the parse-failure semantic is unpinned.

### OPEN #3 (gap) — text/html doesn't narrow to text
`.html` is the `code` family (no Read reader), so text/html rests at binary/htm and only
decodes via explicit `as text`. text/plain narrows fine (text family). Decide if html
should narrow to text (add a code/text Read reader or remap .html).

## Open question (for the reader fallback)
Media readers are **wildcard** (`(image, *)`), so a kind→inner-type that comes
purely from "search the registry for kind X" finds `json`/`csv` but not `jpg`.
The jpg→image step needs the family map (`KindsByFamily`). So "kind → inner type"
spans two sources: the registry's own kind entries + the family map. Where it
lives and what it's named is the thing to settle before coding edit 2.
