# codeanalyzer summary

**Version:** v2

**What this is:** An independent OBP re-check of the item-doors door reshape (decision 184): `git diff 6ff57ceb4^ 12fa4b1bd -- PLang PlangConsole PLang.Generators`, 172 files. Read only. It replaces my v1 F5 (the pairs, the door, PLNG006) and F6's view checks. Report: `v2/report.md`; v1 (the whole-branch audit) is `audit.md`.

**What was done:** I checked log rows 167–183 against the code and ran the named smells and the new rule "Jump to where the code lives" over the diff. The structure holds: no `.pair`, `IOutput`, `WriteReflected`, `OutputTagged` or PLNG006 remains, a value writes itself through double dispatch, `app.Output` is tags plus its guard. Eight findings are fix-at-the-root (R1–R8), four hold with notes (H1–H4). The report ends with every new name for Ingi's naming round and one locked-core proposal (R5a: make `type.list`'s `Of(kind, context)` reachable).

**Next:** the coder fixes R1–R8 at the root (R1's template half waits on 158); Ingi decides the naming round and the R5a proposal. I did not build or run tests.

**Code example:**
```csharp
// R1 today: the format's writer asks the constant, a value asks the view
if (View == global::app.View.Store) { _json.Name("name"); ... }      // wire/kind/plang/writer.cs:39
if (writer.View == global::app.View.Store) { await base.Write(writer); return; } // wire/template.cs:21
// root: the view answers (Keeps the name, Keeps the characters); the writer asks the view, template asks nothing
```
