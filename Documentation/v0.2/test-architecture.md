# Test Architecture

> Decomposed out of `good_to_know.md` (2026-05-31). Content moved **verbatim** — stale pre-rename names are tracked in the `good_to_know.md` index under "Known stale references", not yet swept.

## Test Architecture

### Test Isolation
Each `*.test.goal` gets a fresh app instance. This prevents events, variables, and goal caches from leaking between tests. The fresh app shares the same root directory as the original app.

### Builder Caching
The builder uses a content hash to skip rebuilding unchanged `.goal` files. If a `.pr` file has incorrect data but the `.goal` hash matches, the builder will approve the existing (broken) `.pr`. To force regeneration, delete the `.pr` file and rebuild.

### Test Goal Names
Test goals (`*.test.goal`) must have their goal named `Start` — the test runner looks for a goal called "Start" in each `.test.pr` file. If the goal has a different name, the test runner reports "Goal 'Start' not found".

---

## Mock Module Architecture

The mock module (`mock.intercept`, `mock.verify`, `mock.reset`) provides test isolation by intercepting module action calls at the event level.

### How It Works
`mock.intercept` registers a `BeforeAction` event binding for the specified action pattern. The binding's handler:
1. Captures call parameters into a `Mock.@this.Calls` list
2. If `ReturnValue` is set: sets `context.EventOverride` to skip the real action
3. If `GoalToCall` is set: runs the goal (which can use `event.skipAction`)
4. If neither: spy mode — tracks calls but lets the real action run

### `Mock.@this` (the returned handle)
`mock.intercept` returns `Data<app.mock.Mock.@this>`. The handle's properties are reachable via PLang variable resolution:
- `%mock.CallCount%` — number of times the mock was called
- `%mock.Calls[0].Parameters.path%` — first call's path parameter
- `%mock.Pattern%` — the action pattern being mocked
- `%mock.IsSpy%` — true if no ReturnValue or GoalToCall was set

(Previously named `MockHandle`; renamed to `app.mock.Mock.@this` on the `typed-action-returns` branch. The PLang catalog name still derives to `"mock"` via the @this convention — no PLang-side rename.)

### Builder Naming Gotcha
The handler is named `intercept` (not `action`) because the LLM builder confuses `mock.action` with `mock.mock` — it treats "mock" as both module and action name. Using `mock.intercept` avoids this ambiguity.

### Parameter Matching
Uses regex-based matching: standalone `*` becomes `.*`, regex-like patterns are used as-is, plain strings are exact-matched. Matching is case-insensitive.

---

## Test Module — Cross-Cutting Invariants

The test runner lives in `PLang/app/modules/test/` (`discover.cs`, `run.cs`, `tag.cs`, `report.cs`) and stores run state on `app.Tester` (`PLang/app/tester/this.cs`). Facts future devs won't see in any single file:

### App boundary = file boundary
Each `.test.goal` file gets its own child `App` rooted at that file's directory — not per-goal, not per-step. `test.run` spins up one App via `await using` per `TestFile`, runs the entry goal, then disposes. Multiple goals inside the same file share state within that test's run. Don't "optimise" this by pooling Apps across tests — isolation is the entire point of the module existing.

### Coverage merge is additive + idempotent
`Coverage.Merge(other)` unions module/action observations and branch indices/labels/chains into the parent. `ConcurrentDictionary.TryAdd` makes repeated calls with the same site/label a no-op. This is what makes `test.run` parallel-safe: each child App has its own `Coverage`, merge happens once on completion, no cross-talk.

### Site key = `goalPath:stepIndex`
The branch-coverage site identifier includes the source path, not just the goal name. A `Start` step in two files never collides. The format is fixed by `run.cs:99` and the same format is rendered in the console and `results.json`. Don't change it without updating both seed (`discover.cs:SeedBranchChains`) and observe (`run.cs` AfterAction binding) in lockstep.

### `test.discover` seeds declared branch chains
Before a single test runs, `test.discover` walks every `condition.if` site in every discovered test's goal tree — including statically-reachable `goal.call` targets — and records each site's declared chain on `Testing.Coverage`. Purpose: unreached sites (branches that exist in source but no test visits) still appear in the coverage report. Runtime observation unions in later without overwriting; seed-then-observe is safe by design (`Coverage.RecordBranchChain` stores only the first chain per site).

### `[RequiresCapability]` is class-level, single-instance
Per `PLang/app/Attributes/RequiresCapabilityAttribute.cs`, the attribute has `AllowMultiple = false`. Multi-capability handlers use `params string[]`: `[RequiresCapability("network", "llm")]`. Discovery reflects over the attribute on the resolved handler type for every action referenced in the test's `.pr` (recursing static `goal.call` chains, depth 50, cycle-safe via visited set) and unions the capabilities into the test's auto-tag set. If you add a new capability-hungry action, remember the attribute — otherwise `--test={"exclude":["your-capability"]}` won't filter it out.

### Staleness check uses goal hash, not mtime
`test.discover` re-parses the current `.goal` text into a Goal object and compares `Goal.Hash` (SHA-256 of Name + concatenated Step.Text) against the `.pr`'s stored hash. Touching the file, changing whitespace, or editing a comment doesn't trigger staleness — only changes that affect step text do. Missing `.pr` or unparseable `.pr` also marks Stale with a reason set on `TestFile.StatusReason`.

### `ChildAppCreated` is a test-only hook
`internal static event Action<App> ChildAppCreated` on `run.cs:29` fires once per child App after configuration (SystemDirectory inherited, `Testing.IsEnabled = true`, `CurrentTest` assigned) and before the entry goal runs. It exists so the runner's own meta-tests can install probes observing child-App state (SystemDirectory, parallel count, etc.) without faking. Do **not** depend on it from production handlers — it's an `internal static event` and subscribers must be thread-safe because parallel tests fire it concurrently.

### `test.tag` no-ops outside test mode
Shared goals often tag themselves so they carry auto-tags when reused in tests (`tag this test 'http'`). When that same goal runs in production (no `CurrentTest` on `App.Tester`), the action does nothing instead of throwing. This is why `test.tag` is callable from production goals — it's a one-way signal, never an error.

### `Variables.Snapshot()` honors exclusions, not sensitivity
The snapshot taken on assertion failure (`PLang/app/variables/this.cs:Snapshot`) excludes `!`-prefixed infrastructure vars, `DynamicData` (Now/GUID), and `SettingsVariable`. It does **not** honour `[Sensitive]` — that filter applies at JSON *serialization* via `Json.DiagnosticOutput` when the snapshot is rendered into the report. Result: ordinary user variables carrying secrets flow through the snapshot but are only masked if their carrier type has `[Sensitive]` on the relevant property. See security-report.json finding #3 on this branch.

### Teach LLM mappings via `ExamplesForLlm()`, never via runtime parsers
When a step like `set %count% = %count% + 1` produces the wrong action chain, the temptation is to add an arithmetic evaluator inside `Variables.Resolve` so the runtime "just handles" the `+`. Don't. The compile path already has a `math` module (`add` / `subtract` / `multiply` / `divide` / `power`); the LLM just doesn't know to translate the RHS-arithmetic shorthand. Adding `ExamplesForLlm()` to each math action with both forms (natural — `"add 5 and 3, write to %sum%"` — and RHS — `"set %count% = %count% + 1"`) mapping to `math.<op> | variable.set Value=%!data%` is enough; the LLM follows the example.

The pattern: `static ExampleSpec[] ExamplesForLlm() => new[] { Example("step text", Action("module.action", new() { ["Param"] = ... }), Action(...)) }` — multi-action chains pass multiple `Action(...)` args to one `Example`. Helpers live in `App.Catalog.ExampleHelpers`.

This keeps three things clean: (1) variables stay dumb (regex `%var%` substitution only, no hidden eval); (2) the action graph is explicit — math operations show up as `math.*` actions in the `.pr`, not as inline strings; (3) the catalog is the single source of truth for what the LLM should produce. Stamping the same intent in two places (catalog examples + runtime evaluator) creates drift and is rejected.

---
