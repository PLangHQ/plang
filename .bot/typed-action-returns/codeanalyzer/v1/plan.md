# codeanalyzer v1 — plan

**Task:** Analyze coder's Stage 0 output on `typed-action-returns`.

**Scope:** Production-code diff `c4404b9c5..3c8285760`. Six commits, ~865 insertions across 24 files. Production focus:
- `PLang/app/modules/IClass.cs` (new)
- `PLang/app/modules/builder/code/Default.cs` (RunBuildPass + StampOnTerminalVariableSet)
- `PLang/app/modules/builder/warning/this.cs` (new)
- `PLang/app/channels/channel/noop/this.cs` (new) + `channels/this.cs Channel(name)`
- `PLang/app/data/this.cs` (`As<T>` → internal, new public `As(string)`)
- `PLang/app/Attributes/PlangTypeAttribute.cs` (slim) + `PLang/app/types/Registry.cs`, `types/this.cs`
- `PLang.Generators/Emission/Action/this.cs` (`SetAction` emission)

**Passes:** OBP rules + shape smells, simplification, readability, behavioral reasoning, deletion test.

**Result:** see `report.md`, `verdict.json`.
