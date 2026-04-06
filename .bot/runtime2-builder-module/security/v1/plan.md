# Security Analysis Plan — Builder Module (v1)

## Scope

The builder module (`App.modules.builder`) and supporting infrastructure changes:
- `DefaultBuilderProvider.cs` — core builder logic (8 actions)
- `Goal.Parse()` — .goal file text parser (new)
- `Step.Merge()` / `Goal.MergeFrom()` — merge logic
- `Engine/Modules/this.cs` — action registry (Discover, Describe, GetDefaults)
- `Engine/Providers/this.cs` — named provider registry
- `modules/module/add.cs`, `modules/provider/load.cs` — assembly loading
- `modules/Attributes.cs`, `modules/IConfigure.cs` — metadata attributes

## Approach

### Phase 1: Blue Team (Defensive Audit)
Map attack surface for each area: what's exposed, trust boundaries, existing mitigations, gaps.

Focus areas:
1. **Goal.Parse()** — new text parser. Input validation, resource exhaustion, edge cases
2. **DefaultBuilderProvider** — file I/O, JSON deserialization, path handling
3. **EngineModules.Describe() / GetDefaults()** — reflection, Activator.CreateInstance
4. **Provider registry** — registration, default management, type resolution
5. **Assembly loading** — module.add, provider.load (re-check against threat model)

### Phase 2: Red Team (Offensive Testing)
For each vector: describe attack, assess feasibility, rate severity, propose fix.

### Phase 3: Write Report
- `security-report.json` — structured findings
- `v1/verdict.json` — pass/fail
- `v1/summary.md` — narrative summary

## Threat Model Reminder
- PLang is user-sovereign. .goal files and .pr files are authored/trusted by the user.
- Builder runs on the developer's machine during build phase only.
- Trust boundary = cryptographic signatures on Data, not the builder pipeline.
- Assembly loading from .pr paths is accepted risk (documented in memory).
