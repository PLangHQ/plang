# item-doors — the door reshape, re-checked (decision 184)

Auditor, independent of the codeanalyzer. Range: `git diff 6ff57ceb4^ 12fa4b1bd -- PLang PlangConsole PLang.Generators`
(pieces 6ff57ceb4, cde50a338, bf6353e73, c9cf2199a, 12fa4b1bd; rebuild 27e9f4f4c), against log rows 167–183 and the
rule "Jump to where the code lives" (`Documentation/v0.2/object_pattern_formal.md:100`).

## In plain words

The reshape holds. Every value now writes itself through one non-virtual door, `item.Output(writer) => writer.Write(this)`.
Looks are overrides in the value's own class: a type and a kind in text, an action and an action list in formal. No kind
writes a value. The pairs, `kind.pair`, IOutput and PLNG006 are gone. No `.pr` in `test/` moved, a type in json is
`{"name":"text"}`, and formal arguments are written and read as `[{…}, …]`.

Two things don't hold, and both are worth fixing before merge:

1. **The wire template still decides kept-or-used itself** (`wire/template.cs:21`). It is a seventh view-reading site
   that 177's sweep of six missed. Because of it, a Debug dump of a template container (a `.pr` row like list.query's
   `Query={where: … %min%}`) doesn't show it as written. It shows the decoded structure twice, with each inner variable
   dumped again inside it. 169's "as written → value (type), or not set" is broken for every container template.
2. **The formal reader decides "these are arguments" from the literal's shape, not the slot** (`formal/reader.cs:360`).
   In any of the eleven action slots typed plain `list`, a list of one-key dicts silently becomes a list of named
   values. `llm.query(Tool=[{name: "search"}, {name: "fetch"}])` gives two values named `name`, and the dicts are gone.
   Before the reshape, that literal was refused loudly.

The rest are minor. One is F2 coming back: size and duration make a standard's value through
`type.list[new type("size", standard.Name)]` with the standard already in hand, which is the lookup 181 ruled out.
The others: two view deciders left in plang's writer, `View?` with `??` at the encode doors, Debug's "not set"
swallowing every error, and the number lowering copied into three writers.

## Verdict

| Piece | What | Verdict |
|---|---|---|
| 1 (6ff57ceb4) | the writer holds its view and asker; View objects | **holds**; R4, R5 minor |
| 2 (cde50a338) | a value always writes itself; door, pairs, PLNG006 gone | **holds** except R1 (pre-merge fix at the root) |
| 3 (bf6353e73 + 27e9f4f4c) | formal arguments a list of records | **write holds; read: fix at the root** (R2, pre-merge) |
| 4 (c9cf2199a) | notations are subclasses; a kind writes nothing; host walk | **holds**; R7 minor |
| 183's fixes (12fa4b1bd) | the app's entry for a standard; list Set lifts | **fix at the root** (R3, with F2; not pre-merge) |

Overall: **FAIL**, next is the coder for R1 and R2. Everything else stands.

## The checks asked for

| Check | Result |
|---|---|
| a value always writes itself; looks are overrides in its own class | **holds.** `item/this.cs:842` non-virtual; looks: `type/this.cs:63` (text), `kind/this.cs:308` (text, back to its name), `action/this.Item.cs:68`, `action/list/this.cs:199` (formal). Nothing else overrides the typed writes. |
| a kind never writes | **holds.** No `kind.Write(value, writer)` or `kind.Output(object, …)` left; the 15 number kinds lost `Write`; duration short/iso/dotnet and size iec/si are subclasses writing themselves. A kind held *as a value* writes its name (`kind/this.cs:301`), which is the kind writing itself. |
| no value reads `writer.View` | **one left: R1** (`wire/template.cs:21`). goal, step and property hand the value to the view (`View.Own`/`View.Write`), as 177 ruled. |
| no if/switch/null/cast on the write path beyond the log | **mostly.** Not named: R1's view check; R2's shape test (read path); item's base `value is data.@this` (`item/this.cs:877`), which is minor because `Create` refuses a bare Data; the Assembly fork, moved from the reflection kind (`item/this.cs:858`, was `reflection/this.cs:315`). The `??` view fallbacks are R5. |
| `.pr` bytes didn't move | **holds.** No `test/**/.build` file changed in the range. The five os `.pr` changed only in step text and goal hash (27e9f4f4c, per step). PrByteEqualTests / ActionPropertyPrTests green (below). |
| Debug: `as written → value (type)` / `not set` | **holds for a fresh value and a top-level template row** (show.pr: `"value":{"written":"%template%","value":"not set"}`). **Breaks for a template container** (R1) and for an undecoded nested wire, which is pasted verbatim (R10, the held 158). |
| formal arguments read and write as `[{…}, …]` | **holds** (FormalArgumentTests; show.pr's step text). The read's slot ambiguity is R2. |
| a type in json is `{"name":"text"}` | **holds** (ViewWriteTests; the dumps above write `"type":{"name":"query"}`). |
| anything else a program sees that the log doesn't name | **app's json key order** (R8): `%!app%` written Out now follows declaration order (`id, created, updated, version, module, goal, store, …`), no longer the hand-written order (`goal, type, actor, …`). Nothing pins it. |

## Findings

### R1 — major, pre-merge — the wire template decides kept-or-used itself; a Debug dump of a container template isn't "as written"

- **Why-chain**
  - Debug writes a value that holds variables as written through `Written(value) => value.Output(writer)`
    (`View.cs:68–69`, used at `View.cs:190`).
  - For a `.pr`-read container template that value is a `wire.template` (`type/this.cs:436`). Its write keeps the slice
    only when `writer.View == View.Store` (`wire/template.cs:21`); in Debug it decodes (`wire/template.cs:26`).
  - The decoded entries are Data holding sources with variables. Each goes through Data.Output's HasVariable branch
    (`data/this.Output.cs:55–66`) and is dumped as `{written, value}` again.
  - Then "value" settles the same template (`View.cs:194`) and writes the same decoded structure a second time.
- **Probe** (scratch worktree, `test/module/list/query/.build/wherevariable.test.pr` step 2, `%min%` unset):
  `"Query",…,"value":{"written":{"where":{…,"value":{"written":"%min%","value":"not set"}}},"value":{"where":{… same …}},"type":{"name":"query"}}`
  - "written" is not as written.
  - The container is never "not set".
  - The dump doubles.
- **missed_by:** the 177 sweep. It named six view-reading sites (app ×2, goal.Item, step.Item, identity ×2, property),
  not this one. ViewWriteTests only write fresh `text` templates, never a `.pr`-read `wire.template`.
- **Also:** the comment at `template.cs:19–20` says "the same check as source.Output's". Source's check is gone (its
  write is now always as written, `source.cs`), so the two template carriers now disagree.
- **Fix at the root:**
  - A template writes as written in every view: drop the override and inherit `wire.Write`, whose Owns/Raw keeps the
    slice for plang and json.
  - Using it is the view's alone. Out's `Settled`/`Opened` (`View.cs:73`, `:141`) already decode.
  - Red: a Debug dump of a `.pr`-read container template shows `"written"` equal to the authored slice, and `"not set"`
    when its variable isn't.

### R2 — major, pre-merge — the formal reader tells arguments from data by the literal's shape, in every plain `list` slot

- **Why-chain**
  - `formal/reader.cs:360`: `declared.Type.Name == "list" && value.Items is { Count: > 0 } records && records.All(r => r.Entries is [_])`
    then reads the list as argument records (`Arguments`, `reader.cs:436`).
  - Plain `list` is the declared type of eleven slots, not just the four Parameter slots (goal.call, ui.render,
    window.call, terminal.start/open): llm.query `Tool`, test.tag `Tags`, task.wait `List`, build.pick `Popular`,
    signing.sign `Contracts`, terminal `Keep`.
- **Probe:**
  - `llm.query(Message="hi", Tool=[{name: "search"}, {name: "fetch"}])` → Tool's elements are Data named `name`
    holding `search`/`fetch`; the dicts are gone.
  - `test.tag(Tags=[{name: "a"}, {name: "b"}])` → the same.
  - `Tool=[{name: "search", description: "x"}]` → a dict, as meant.
- **Before:** at 6ff57ceb4^, `reader.cs:~503` refused any `[{ident: …` literal in a plain list slot, loudly. Now the
  one-key case changes the data silently, and the builder's model writing a list of one-key dicts never learns.
- **The rule:** an `if` on what the literal is, a test on a type's *name* (`Type.Name == "list"`, string address). The
  reference the decision should jump to is the slot's declared type: "a list of arguments" is what goal.call's
  `Parameter` *is*, not what its literal looks like.
- **Fix at the root:** the argument slots declare it (their type says a list of records), and the reader asks the
  declared type. That touches the type system (locked), so it is a proposal for Ingi. Until then, scope the shape test
  to the slots that take arguments, with a red for `Tool=[{name: "x"}]` staying a dict.

### R3 — minor — a standard's value is made through a type built from names, with the standard in hand (F2 again)

- **Why-chain**
  - `size/this.cs:38–39`: `Of(bytes, standard, data) => type.list[new type("size", standard.Name), ctx].Make(…)`.
  - `size/this.cs:65–66`: the asked kind (already an object, `declared.kind`) is compared to the value's kind by name
    and looked up again by name, `kind[asked.Name]`.
  - Duration does the same at `duration/this.cs:60–62` and `:47` (`new type("duration", "short")`); so do the readers,
    `size/serializer/Reader.cs:20–21` and `duration/serializer/Reader.cs:20–21`.
- **The log contradicts itself:**
  - 181: "a size made from a count in a standard is made by that standard itself … not through
    `type.list[new type("size", standard.Name)]`, the by-name lookup both audits flagged".
  - 183 then asked for "the app's entry, not a fresh type (33)".
  - 184 met 183 with exactly the lookup 181 ruled out.
- **Root:** F2. A kind has no reference to the app's entry for {its family, itself}, so every holder rebuilds the
  address from names.
- **Fix at the root, with F2 (Ingi):** the kind answers its entry (or *is* it), and `standard.Make(bytes, data)` is the
  one jump. Not pre-merge on its own: it is behaviour-correct (StandardRoundTripTests, the 184 red).

### R4 — minor — plang's writer decides by view identity where the view already answers

- `plang/writer.cs:39` writes the record's name when `View == View.Store`.
- `plang/writer.cs:74`: `Sensitive(characters)` keeps or masks by `View == View.Store`.
- Masking is already the view's (`View.Masks`, `View.cs:33`). Two deciders for one question; `IWriter.Sensitive`'s
  default always masks, whatever the view.
- **Fix:** the view answers (a member the writer asks), so the writer has no `==` on a view.

### R5 — minor — the encode/decode doors take `View?` and fall back with `??`

- `goal/this.Item.cs:27`, `plang/this.cs:31` and `:74`, `json/this.cs:55`, `text/this.cs:63`, `ReadContext.cs:37`
  (`View? View = null`, "null is Out"). 17 `View?` parameters.
- This is forced by the enum becoming objects (no constant default), and it is a null check at every start.
- 175 said only where a write starts names a view.
- **Fix:** the encode door takes a non-null view, and the callers that relied on the default name `View.Out`.

### R6 — minor — Debug's "not set" swallows every error; a throwing [Debug] getter ends a dump

- `View.cs:194–196`: `catch (AppException) { used = null; }` and any `!Success` → `"not set"`. A value that resolves to
  a real failure (a render's type error, a reference to a failed Data) reads as not set, and its error is lost.
  169 meant "doesn't resolve".
- `View.cs:172–181` (`debug.Own`): `property.GetValue(value)` is unguarded. Item's base write wraps a throwing getter
  (`item/this.cs:866–873`, `OutputGetterThrew`), but this one escapes, while "a dump never throws" (`View.cs:167`).
- **Fix:** "not set" for a missing name only; any other failure is shown as its error. Guard the getter as the base
  write does.

### R7 — minor — the number lowering is copied into three writers

- The same 11-case switch (byte … BigInteger) is added to `json/writer.cs:85–95`, `text/writer.cs:115–125` and
  `formal/writer.cs:184–194`.
- `number/this.cs:55` hands its boxed CLR value to `writer.Value(_value)`. Before, each number kind emitted its own
  token (stored once, in the kind); now how a CLR number lowers lives in three writers.
- 174 accepted `writer.Value(…)`, so not a reversal, but it is stored three times.
- **Fix, deferred:** one owner of the CLR-number lowering that the three writers share (IWriter is locked, so a
  proposal).

### R8 — minor — app's Out key order changed, unnamed

- `app/this.cs`: the hand-written write (goal, type, actor, error, test, shortcut, module, store, parent, id, created,
  updated, version) became the tagged walk in declaration order (id, created, updated, version, module, goal, store,
  test, error, shortcut, type, parent, actor).
- Same members, same views. A program reading `%!app%` as json sees the keys reordered. Nothing pins it.
- **Fix:** name it in the log (or order the members); no code change needed.

### R9 — nit — kind's text override is the old undo pair, as a virtual

- `kind/this.cs:308`: `Write(text.Writer) => Write((IWriter)writer)` exists only to turn type's text look back off for
  a kind, as `kind/format/plang.cs` did (my F3's "anti-pair").
- Named in 178 ("kind's name"). Its root is "a kind is a type"; no fix now. Recorded so the naming round sees it.

### R10 — note (158, held) — an undecoded nested wire is pasted verbatim into a dump

- In a Debug json dump of `os/system/error/.build/show.pr`, `Parameter` (a plain-list wire, not template-marked) is
  written as its `.pr` bytes: `[{"name":"error","type":{…},"value":"%error%","variable":[…]}]`, with no
  `→ value / not set`.
- `wire/this.cs:62`: `_reader.Owns(writer) → Raw`. An Out json write of the same step would carry the store form too.
- This is F6 / 158 c2, held by 174. Not new, but 169's Debug shape doesn't reach it until 158 is built.

### Outside the reshape (noticed, not traced)

A Data stored in plang (Store, signed) and decoded with `View.Store` comes back with its text template's variable list
empty. Probe: `hasVar=False, template=plang`, while the stored bytes carry `"variable":[…]`. The read side
(`data/reader/this.cs:85`) is untouched by the range. Worth a separate look: a stored template read back is never seen
as holding variables.

## Previous reviews assessed

The codeanalyzer's report was not read (LIMITS). The log's review notes (176–183) agree with what I found, except where
R1 (the sweep's six sites) and R3 (181 vs 184) diverge.

## Tests (at 12fa4b1bd)

- C#: Types 906, Data 846, Generator 191, Runtime 1081 green; Modules 1300 green but one timing flake under load
  (`Sleep_LastsItsDuration(500ms)`, 10478 ms against a 10000 bound; SleepTests 5/5 when run alone); Wire 6 of 546,
  the same six snapshot tests as before.
- plang: 303 total, 277 pass, **11 fail**, 12 stale, 3 skipped. The same 11 by name as at db99d02ae.
- Probes ran in a scratch worktree (`/workspace/item-doors-probe`, removed). Nothing committed; production source
  untouched.
