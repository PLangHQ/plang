# Security audit v1 — runtime2-builder-bootstrap

## Scope

Branch is huge (~2500 files / 200 C# files changed vs runtime2). I am scoping to
the new and substantially-changed surfaces that touch trust boundaries:

**Tier 1 (deep dive)**
- `PLang/App/Errors/ParamSnapshot.cs` (NEW) + `PLang.Generators/LazyParamsGenerator.cs` `__SnapshotParams` block — captures handler parameter values into errors. Sensitive-attribute interaction.
- `PLang/App/Variables/this.cs` — `Set` rewrite, JSON deep-clone fallback, `GetAll` shape change, `Snapshot` (standing finding).
- `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs` — `BuildingGuard` removed across every action; new path-filter logic; CLR-type-name guards.
- `PLang/App/Utils/TypeConverter.cs` (NEW, ~400 LOC) — type-confusion / deserialization gateway.
- `PLang/App/Errors/Error.cs` — verbose dump, ParamSnapshot rendering, variable rendering.

**Tier 2 (lighter scan)**
- `PLang/App/Catalog/*` (NEW) — content composed and sent to the LLM.
- `PLang/App/modules/ui/providers/FluidProvider.cs` — new `formal` filter, `UnwrapFluid` recursion.
- `PLang/App/Debug/this.cs` — granular LLM tracing, file-output path construction.
- `PLang/App/modules/llm/providers/OpenAiProvider.cs` — schema serialization, RawResponse-in-Error.Details.
- `PLang/App/Modules/this.cs` — Describe() output (LLM input).

**Out of scope**
- .goal / .pr / docs / test fixtures
- Builder LLM prompts (system/builder/llm) — design domain, not C# attack surface.

## Threat-model framing

Trust boundary is the cryptographic signature on Data and on .pr files. Once a
.pr is signed and verified, everything inside runs with full user trust. I rate
findings against:

1. Untrusted-data leakage out (sensitive properties, secrets, identity privates).
2. Unhandled deserialization / type confusion on values that originate from
   external channels (HTTP, file, LLM responses).
3. Resource exhaustion through unbounded recursion or unbounded inputs.
4. Trust-boundary regressions — code that lets an unsigned mutation through.

## Standing findings to verify on this branch

- `Variables.Snapshot()` doesn't honor `[Sensitive]` — leaks into AssertionError → results.json.
- `Variables.GetAll()` doesn't honor `[Sensitive]` — leaks into Error verbose dump.

## Plan

1. Read Tier 1 files, trace every new path that touches values which could carry secrets.
2. Confirm or refute the standing findings — note any new vector (`ParamSnapshot` is a candidate).
3. Light scan of Tier 2 for obvious red flags (recursion w/o depth, unbounded input, missing filter).
4. Produce `security-report.json` at branch root + `verdict.json` in v1.
