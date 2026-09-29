# coder v12 — stage 10d: formats live with their owners

Contract: `test/plan/app-systems/start.goal` Stage10d, decision 203 (Ingi: "yes", and "yes, disolve").
The moves are namespace moves (`git mv`, then each referencing production file edited one at a time, test files
batched). Each slice is a commit, reported to the architect.

## What is there
`PLang/app/type/format/` holds the concept and the machinery:
- concept: `this.cs`, `IWriter`, `IReader`, `IOutput`, `value/reader`;
- `json/{reader,writer,converter}` (23 production files and 19 test files reference `type.format.json`);
- `text/writer` (2 + 2);
- `formal/writer` (10 + 2), whose reader is `goal/step/action/serializer/Formal.cs`;
- `filter/Tagged` (2 + 2) and `filter/Sensitive` (1 + 1).
- `Sensitive` is used by `Diagnostics/Format.cs:34` (Mask) and `module/debug/this.cs:419` (Strip), both raw STJ modifiers.
- Test's `[PlangType("format")] enum Format {Json, JUnit}` is the `test.setting.Format` choice; `test/report/this.cs:70` forks `if (chosen == Format.JUnit)`, and `test/junit/this.cs` builds the XML.

## Slices
1. **json → `type/item/kind/json/`** (namespace `app.type.item.kind.json`, beside the json kind): Reader, Writer, Converter.
2. **text writer → `type/item/text/`; Tagged → `type/item/kind/reflection/`; the formal writer and reader → `goal/step/action/formal/`** (the action's notation, not a format).
3. **Sensitive dies**: `Diagnostics.Format` and debug's raw serializer write through the json writer, whose `[Sensitive]`/`[Masked]` handling is the one filter.
4. **Test's Format dissolves**: the report's format is a format kind — json exists, junit becomes one (the XML moves from `test/junit` into the kind's encode). `test.setting.Format` names a kind; `report.Write` asks the kind to write its own artefact, with no fork.

`app/type/format/` ends as the concept only: `this.cs`, `IWriter`, `IReader`, `IOutput`, `value/reader`.

## Questions (to confirm at slice 3/4, not blocking 1–2)
- Slice 4: `test.setting.Format` is a `choice<Format>` today. Replace it with a kind name (text, validated against the kinds that write a report), or `choice` over a set built from those kinds?
