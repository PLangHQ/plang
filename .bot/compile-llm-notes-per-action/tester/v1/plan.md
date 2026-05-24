# Tester — compile-llm-notes-per-action — v1 plan

## Context
Pipeline state on entry: architect ✓, test-designer ✓ (22 C# tests + 2 plang drift cases, all failing-by-construction), coder v1 committed (`067c1e1b7`) + a follow-up builder tweak (`5253acf05`). No prior tester pass.

The architect's verification rule for the **two plang drift cases** is "3 fresh-cache builds in a row" (CLAUDE.md "Stale-binary trap"). My job is to confirm both the C# mechanism layer and the drift layer hold against a clean build, and to mutation-test the load-bearing assertions so I'm not just trusting green-by-construction.

## Steps

1. **Clean rebuild from zero** — `rm -rf {PlangConsole,PLang,PLang.Tests,PLang.Generators}/{bin,obj}` then `dotnet build PlangConsole`. Avoids the stale-binary trap.
2. **Full C# suite** via the TUnit binary `PLang.Tests/bin/Debug/net10.0/PLang.Tests` (the project is a Library, not Exe — `dotnet run` won't work; `dotnet test` is blocked by the .NET 10 SDK VSTest deprecation. The binary itself is the runner).
3. **Targeted runs of the five new C# test classes** via `--treenode-filter`:
   - `MarkdownTeachingLoaderTests` (6)
   - `MarkdownTeachingMergeTests` (4)
   - `MarkdownTeachingOrphanTests` (3)
   - `StepActionDetailsRenderTests` (6)
   - `CodeAttributeRegressionTests` (3)
4. **3-fresh-cache drift validation** per architect: `rm -rf Tests/Simple/.build && plang build /Tests/Simple && cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test Builder/CompileLlmNotes` ×3.
5. **Mutation tests**: break each load-bearing rule (merge order, module-stem orphan exclusion, loader layer split, renderer planner-set gate); confirm the test catches it; revert.
6. Write artifacts: `plan.md`, `verdict.json`, `verdict.md`, `summary.md`, append session to `report.json`.

## Decisions

- Run drift cases only after C# suite passes (cheaper failure surface first).
- Mutation tests target the smallest behavior the test pins — single-line edits, immediately reverted. Announce once per batch per CLAUDE.md "Mutation Testing" rule.
- Do not delete `Tests/Simple/.build/` permanently — that's tracked content. Restore with `git checkout HEAD -- Tests/Simple/.build/` after each fresh-cache attempt.
