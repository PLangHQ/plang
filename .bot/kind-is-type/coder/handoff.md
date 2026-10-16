# Coder handoff — kind-is-type (stages 4–6 closed)

HEAD: `99f6b629f`. Branch closed except TheFilePage (the builder's) and stage 5 (the builder's, paused).
A kind is a type under its parent (`kind<T> : kind : type`); names are unique at their own level; a
format type owns its bytes. Stages 0–3 landed earlier; this handoff covers **stages 4–6**.

## What landed (stages 4–6)

### Stage 4 — a format type owns its bytes (json migration, 773/786/787/792/804/805/818)
- **json rests, a use opens.** `json.Load` returns an unread `{item, json}` source holding raw bytes;
  `source.Value` is the courier door (a resting source answers itself, carried unread). A USE opens it
  once and narrows (787): `data.Open()` parses through the kind, `_item` rebinds to the parsed dict/list,
  the source rides as history (parse-once). Use-sites: `source.Get` (nav), `data.EnumerateItems`
  (foreach), `data.Compare`/`data.Set` (operands/parent), `Value<T>()` (a made-into-T unread source),
  the `is` operator (`Operator.IsType`, 8fc6e1b13), and llm.query with a Schema (d61eea852).
- **json is item's kind, not text's (773).** text-of-kind-json retired; a json value is `{item, json}`.
- **The navigator is gone (2622f5514):** json kind lost Descend/Data/Enumerate/IsSequence/Set/Clr/
  Output/Write/Scalar/ClrForm; the `clr{JsonElement}` branches left dict/list/kind.dict/kind.list and
  http. **The text wrapper + `*format` coiner are gone (d6d92efc2):** `source` carries the format kind
  object through `Declared` (`_format`); `type.list.Kind(string)`'s by-name entry deleted (ruling 2 —
  a kind is asked of its parent; `Held`'s scan kept for MIME/extension).
- **render (7574cd6d3):** a value's text face asks `_type.kind.IsText`; `ui.render` hands rendered chars
  to the declared format's own birth. **the record read (789/790):** the item owns its read; the
  start-up settings-reader loop + per-type reader gone; an unreadable old row is loud.
- **Close:** stage-4 plang tests green (json/PassedOnUnread, ReadIntoIsADict, ArrayIsAList,
  csv/ReadIntoIsATable; C# TypedSlotJsonReadTests); the `test/plan/kind-is-type/app/type` subtree 11/11.

### (a) 814 — host kinds are Internal (ff2c44069)
`Internal` is a fact a kind answers for itself: new `OwnInternal` hook on `type` (`OwnInternal ??
kind.OwnInternal ?? Family._internal`, the 777 pattern). item's `dict`/`list`/`*` host kinds override
it true, so a kinded type `{item,dict}` answers Internal through its kind and the builder's
`KindsOffered` (`!k.Internal`, `goal/step/pick/list/this.cs:640`) leaves them out; `{item,json}` stays
offered. A bare `static Internal` on the kind class does NOT work — `type.Internal` reads the family's.

### (b) file.read Type slot (ff2c44069)
`data<type>? Type` mirroring variable.set. With a Type, the bytes are read now and the declared type
births from the raw `byte[]` (pass `.Value`, not the binary carrier — text.Create decodes a byte[] at
`text/this.cs:379`), the file its origin; the landed value IS that type (`%r!type.name%` text), not a
file reference. Verified: `FileHandlerTests.Read_AsText_LandsTheContentAsText_NotAFileReference`.

### Stage 6 — the leftovers (5b19121e6, 8cc2a5c65)
- **Stopgap 80e4c9e4e gone:** `%x!type.kind%` is answered directly (a kind is an item), no clr-carrier
  round-trip + unwrap.
- **Codeanalyzer cleanups (82d8866ae):** dead `TypeDescription` const removed; stale `BuildTypeEntries`/
  `Promote` doc fixed; `subclass.Offers` binds its class's static `Offer` once at construction.
- **F4.2 (8cc2a5c65, 833):** every kinded type's face is `name<kind>` — `ToString => kind.IsEmpty ?
  Name : $"{Name}<{kind.Name}>"`, no family name-switch. The face is the type's; which faces a prompt
  shows is the builder's.

## On branch A's list (deferred here by decision 833, with findings)
Branch A reshapes how a value is read/made through its type — "one make door" (789/796).
- **F8.1 — the three "is this an action slot?" name checks** (`goal/step/action/this.Validate.cs:54`,
  `formal/reader.cs`, `action/serializer/Reader.cs:153`: `Name=="action" || (Name=="list" &&
  kind.Name=="action")`). **Finding:** `Takes` answers *conversion* ("can a value of X be made into
  this") — `action.Takes(action)` is false (no ICreate.Takes override), `list<action>.Takes(action)` is
  false (`list.Takes(other) => other.Is("list")`). Making `Takes` also answer "is this slot
  action-typed" would give one door two meanings. The fix (branch A): the slot's type reads/checks its
  own value (action reads action rows; list<action> through its element), so no site asks by name. The
  three name checks stay for now.
- **#3 the floor** (`type/item/source.cs:115`: `_type.kind.IsText || _type.Name is "text"`).
  **Findings (each isolated and verified; an earlier guess that "the registered type drops Template/
  origin" was WRONG — `type/list/this.cs:167` `Full` preserves `Template`, and `origin` is passed
  explicitly):**
  - Born the Make wire-raw source holding the full type (`type.list[this, context]`) **alone** regresses
    **nothing** (Types 852/0, FileHandlerTests 34/0). It is a safe change, not the regressor.
  - The ~13 regressions (Read_TemplateTrue, AFile*/AUrl* content-as-template) come from **removing the
    floor**. The floor compensates for text's own format being declared `[Format("", "text/plain",
    ".txt")]` with **no `Text = true`** (unlike xml/ini/goal/…), so text's empty kind answers
    `IsText = false`.
  - But `Text = true` on text's own format, even together with #3, still regresses the template/file
    tests — so the floor's role is deeper than plain-text `IsText`: the content/template decode path
    reads `Peek()` expecting the decoded **string**, and a false `IsText` there makes `Peek()` return raw
    bytes, breaking template rendering.
  - So the floor's removal needs the "born type + kind + format are one shape" work — branch A's "one
    make door". Two concrete leads for branch A: text's own-format likely should be `Text = true`, and
    the decode/`Peek` path should get the decoded string without leaning on a type-name check. The name
    floor stays here, recorded.

## The review line's starting point

- **Green:** Types/Data/Generator/Runtime, and Modules but TheFilePage. The plang `app/type` subtree is
  11/11.
- **Carried baseline (NOT this branch):** the 6 Wire snapshot/resume tests (ThrowTimeSnapshot,
  MidStackChain, NavigateAndEdit, PlangPath_AsSnapshot, SerializedString, TypedSnapshotString) — fail
  identically on runtime2 (`cf66e2d01`, per the architect).
- **The builder's (paused, left red deliberately):** `TheFilePage_IsTheSpecsGolden` — file.read's Type
  renders on the file module page but `read.notes.md`'s Type teaching, the `ask:` that routes `as text`
  to the slot, and the module page golden (`Documentation/v0.2/module-reference-generation.md`) are the
  builder's stage-5 teaching, re-pinned in its single pass with the decider fixtures. The plang
  `AsTextKeepsText` also waits on that routing (the `.pr` must carry `Type=text` on file.read). The
  orphaned `separator` block in `type-reference-generation.md` is the docs bot's.
- **Decider pins for the builder's resume:** F4.2 moves `ThePromptCUserMessage` + `StageOnesAnswer`×2
  again whenever stage 5 changes a type face; the builder re-pins them in its pass.
- **Stage 5 (the builder offers kinds):** the builder's, paused (`.bot/kind-is-type/builder/handoff.md`).

## Review focus
The shape work: json rests + a use opens (one `data.Open` door); a kind is a type answering its own
facts (`OwnInternal`, the 777 `Own*` pattern, ToString face); `Kind(string)` by-name lookup gone
(ruling 2). The two deferred items (F8.1, #3) are recorded as branch A's — the name checks and the
text-face floor are intentional holds, not misses.
