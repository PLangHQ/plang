# codeanalyzer v2 — plan

**Task:** Analyze coder's Stages 1-4 + bonus work on `typed-action-returns`.

**Scope:** Production-code diff `68319f649..9513a6fe7`. 15 commits, ~770 production insertions across ~51 files.

**v1 status:** Stage 0 flagged 7 LOW + 1 design call. Not re-reviewing Stage 0 in v2 unless the new commits touch the same lines.

**Priority files (per coder handoff + diff sizing):**

- Renames / new owners:
  - `PLang/app/tester/Test/this.cs` (renamed from `tester/File.cs`)
  - `PLang/app/builder/Types/this.cs` etc. (renamed from `modules/Schema/`)
  - `PLang/app/mock/Mock/this.cs` (renamed from `modules/mock/types.cs MockHandle`)
  - `PLang/app/http/Response/this.cs` (new record)
- Typed Run() signatures:
  - `modules/test/discover.cs`, `modules/test/run.cs`, `modules/output/ask.cs`, `modules/mock/{action,reset,verify}.cs`, `modules/builder/{types,actions,goals}.cs`, `modules/goal/getTypes.cs`, `modules/http/{request,upload}.cs`
- Build() impls (Stage 4):
  - `modules/file/read.cs`, `modules/llm/query.cs`, `modules/http/HttpBuildHelpers.cs`
- Bonus — Serializers return Data:
  - `app/channels/serializers/serializer/{this,Json,Text,plang/this,plang/Data}.cs`
  - `app/channels/serializers/this.cs` (registry)
  - `app/channels/channel/{message,stream}/this.cs` (consumers)
- Bonus — HTTP body dispatch via registry:
  - `modules/http/code/Default.cs` (ParseResponseAsync + TextFallback)
- Touched consumers:
  - `app/types/path/this.Authorize.cs` (Ask.ShouldExit)
  - `app/types/path/file/this.Operations.cs`
  - `app/types/this.cs` (PlangType derivation)
  - `app/modules/this.cs` (Describe)
  - `app/channels/this.cs`
  - `app/goals/this.cs`, `app/data/ShouldExit.cs`
  - `app/Utils/PathHelper.cs` (Extension no-dot)
- Tiny edits across math/error/llm/settings/test — cross-check for footgun pattern.

**Passes:**

1. **OBP rules + shape smells** (incl. new leaf-returns-Data rule, System.IO/Console grep)
2. **Simplification** (especially the rewritten Serializer impls + ParseResponseAsync)
3. **Readability** (naming after the renames; Run.File property kept)
4. **Behavioral reasoning** — focus on:
   - `Data<object>` implicit-operator footgun on every forwarder
   - `Ask` ToString rendering / null-Answer behavior
   - `Response.Body` type discipline across content-type branches
   - `path.Extension` no-leading-dot — every caller migrated?
   - Build() inference vs (type) hint precedence in `Default.cs:StampOnTerminalVariableSet`
5. **Deletion test** on fix-introduced code (especially Serializer try/catch blocks).

**Result:** see `report.md`, `verdict.json`.
