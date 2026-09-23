# to-architect — graft typing: an untyped row is born typed at the graft; NormalizeParameterTypes dissolves

## Where the graft is today (traced)

`set %goal.step[i].action% = %properties.step[i].action%` → `step.Set("action", …)`
(`goal/step/this.Item.cs:53`) → each row `row.Read(actionReader)` → action reader `Populate`
→ `"parameter"` arm → `Parameter(raw)` (`action/serializer/Reader.cs`) → `data.reader.Read(raw)`.

The data reader **throws** on a row with no `type` (`data/reader/this.cs:75-81`, "value slot has no
declared type"). The stage-3 LLM answers rows WITHOUT types (`{"name":"Path","value":"notes.txt"}`),
so **an LLM answer cannot graft today**. Only `build_pr.py` gets through — it stamps the declared type
itself before writing the .pr. `NormalizeParameterTypes` (`build/code/Default.cs:341-470`) runs
AFTER the graft, on rows that could not have been read without a type.

## What NormalizeParameterTypes does (seven jobs) and where each goes

| # | job | goes to |
|---|---|---|
| 1 | stamp each row's type from the handler's CLR property type by reflection, **overriding** the LLM's | the graft: the row's type IS the element's declared `Property` row type (`property.Type`, already reflected once in the catalog) |
| 2 | kind probe: build the literal through the declared type to stamp its kind (png → image/png, 5 → number/int) | the graft: a literal row reads EAGERLY through its declared type's reader; the value carries its own type+kind |
| 3 | `""` on a nullable slot → unset | the graft: an empty literal on a nullable declared slot is born a declaration (no value) |
| 4 | skip catalog descriptions (`"int = 1"`) | **dies** — only the "feed the catalog back through validate" smoke test hit it |
| 5 | skip scalar plang types (path) and [Choices] types — keep the string on the wire | nothing to do: the typed read keeps the token verbatim on the wire; only the in-memory value is typed |
| 6 | convert a literal to its declared type; failure → a build error | the graft: the eager typed read IS the conversion; a decline is the row's own read failure → `Apply` fails → `Settle` retry → `FixProperties` |
| 7 | template flag: a value containing `%var%` gets `type.template = "plang"` | the graft: a string row carrying `%var%` is born `{declared type, template: plang}` and stays lazy (never converted at build — judging must not resolve) |

## The mechanism (in the action reader, which knows the element)

1. `Populate` collects the raw `parameter` rows and reads them AFTER `EndObject` — `module`/`name`
   may follow `parameter` in a wire; the element (`Module[Name]`) must be known first.
2. `Parameter(raw)` → one row door:
   - row has `type` → as today (`.pr` rows are always typed; `action`-typed rows keep the held-action arm);
   - row has NO type → the declared `Property` row for its name gives the type:
     - value is a string with `%var%` → `{declared, template: plang}`, lazy (job 7);
     - value is `""` and the declared slot is nullable → a declaration row, no value (job 3);
     - otherwise → the literal is read EAGERLY through the declared type (jobs 1, 2, 6); a decline
       throws the row's own read error with the action + param named.
   - row has no type AND no declared row (a `goal.call` argument inside `Parameter`, where the slot
     is `list` and the callee declares nothing at build) → the token's own type (string → text,
     number → number, bool → bool, object → dict, array → list), `%var%` → text template.
3. `NormalizeParameterTypes` and `IsCatalogDescription` are deleted; `build.validate` keeps
   defaults fill → `step.Validate` → the node-owned `Build`.

## Questions

1. Job 1 overrides an LLM-supplied type with the declared one ("the schema is the contract"). At the
   graft, should a row that DOES carry a type still yield to the declared one? I'd say yes when the
   declared type is concrete, keep the row's when the slot is `item` (bare `Data`) — `variable.set`
   `Value` is `item`, where `set %x% = 5 as text` must keep `text`.
2. Job 2 eager read at the graft: the eager read happens under the BUILDER's context. A literal whose
   type needs I/O to read (a `path` probe, an `image` kind sniff from a real file) — build-time I/O. Today
   job 2 already does this (`entity.Create(p.Peek(), carrier)`). Keep, or restrict eager read to
   types whose read is pure (the scalar families) and leave I/O-backed kinds lazy?
3. Nested argument rows (goal.call `Parameter` items): token-typed as above, or should `call.Build`
   type them from the callee goal's own declarations once the target is found (A1's canonicalization
   already finds it)? Token-typed is enough to read; callee-typed is more honest but the callee's
   "declarations" are just the variables it reads — there is no signature to read today.
