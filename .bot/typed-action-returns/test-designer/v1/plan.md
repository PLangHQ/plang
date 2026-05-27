# test-designer plan — typed-action-returns v1

**From:** test-designer
**Inputs:** `.bot/typed-action-returns/architect/plan.md` (v3.3), `.bot/typed-action-returns/architect/stages.md` (5 stages: 0 foundation, 1 rename, 2 mechanical typings, 3 HTTP Response, 4 Build()+hint)

This branch carries the whole Branch-A pipeline (typed-action-returns). Branch B (`test-report-typed-object`) is forked off `runtime2` separately and not in scope.

## Approach

- One v1 test contract covering all 5 stages, organized by stage.
- Tests are **signatures only** — `Assert.Fail("Not implemented")` for C#, `- throw "not implemented"` for `.goal` bodies. Coder makes them green.
- Per the character file: present high-level plan first, then propose tests in batches of ~10, wait for approval per batch before moving on. Once all batches approved, materialize files.

## High-level test plan (batches)

| # | Stage | Area | Approx tests | Mix |
|---|---|---|---:|---|
| 1 | 0 | `[PlangType]` removal + class-name derivation | 6 | C# |
| 2 | 0 | `IClass.Build()` interface + validate-pass plumbing | 8 | C# + 1 PLang |
| 3 | 0 | Named channels + `BuildWarning` + no-op fallback | 9 | C# |
| 4 | 0 | `Data` materialization owned by `.Type` (Data.As(string), no generic As<T>) | 7 | C# |
| 5 | 1 | `tester/File` → `tester/Test` rename + consumer sweep | 6 | C# + 1 PLang |
| 6 | 2 | Mechanical typings — test.* + goal.getTypes + output.ask + channel.set | 10 | C# + PLang |
| 7 | 2 | Mechanical typings — mock.intercept + builder.{types,actions,goals} + test.tag | 9 | C# + PLang |
| 8 | 3 | HTTP `Response` record + typed `http.request`/`upload` returns | 6 | C# |
| 9 | 3 | HTTP runtime Content-Type body dispatch | 8 | C# + 1 PLang |
| 10 | 4 | `file.read.Build()` + `llm.query.Build()` + `http.*.Build()` | 11 | C# + PLang |
| 11 | 4 | `(type)` hint Compile rule + multi-segment `GetByExtension` + precedence | 8 | C# + PLang |

**Approx total: ~88 tests across ~11 batches.** Numbers may drift ±2 per batch as we go.

## C# vs PLang split

- **C#** carries: interface shape, type derivation, channel mechanics, Data materialization API, Response shape, runtime body dispatch, Build() per-action return values, registry extension. These are internal — testable directly without going through PLang syntax.
- **PLang `.test.goal`** carries: end-to-end trace snapshots showing `(object)` → `(T)` flip, build-time warnings appearing in trace, `(type)` hint precedence end-to-end. These are the user-visible contract.

PLang tests live under `Tests/TestModule/TypedReturns/` (new) — one folder per stage to keep `.pr` snapshots scoped. C# tests live under `PLang.Tests/App/TypedReturnsTests/` with one file per batch (e.g., `Stage0_PlangTypeDerivationTests.cs`).

## Coverage discipline (per architect plan)

- Each typed handler in Stage 2 gets: (1) one C# unit confirming `Run()` signature returns the right T, (2) one PLang `.test.goal` confirming the trace snapshot shows the typed annotation downstream.
- Each Build() in Stage 4 gets: (1) one C# unit per branching condition (literal hit, literal miss → warning, non-literal → Ok()), (2) one PLang `.test.goal` confirming end-to-end terminal Type.
- Channel + Data API gets thorough C# coverage (no PLang surface).
- The `[PlangType]` attribute removal gets a compile-fail-style test (try to use it via reflection — assert it doesn't exist).

## Interactive flow

I present batch 1 below. After approval (or with changes), I present batch 2. Continue until all 11 batches approved, then write files in one pass.

## Blockers

None — architect's design is locked at v3.3, scope is clear, stages have explicit acceptance criteria.
