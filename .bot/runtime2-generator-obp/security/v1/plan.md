# Security v1 — runtime2-generator-obp

## What I'm reviewing

Big branch (1981 files) — mostly the v4 OBP refactor of the source generator
and the resolution-as-read-transformation move. Coder went through 4 rounds,
codeanalyzer cleared v3, tester approved v4. My job: red-team the security
delta, not the whole runtime.

## Surface

1. **`Data.As<T>(context)`** (`PLang/App/Data/this.cs:383`) — the new
   resolution entry point. Replaces the old `Data.Value` side-effect getter.
   Walks `%var%` refs, deserialises, returns `Data<T>`.
   - Cycle protection: `[ThreadStatic] _resolvingValues` HashSet +
     `ResolveDepthLimit=32` for expanding chains where each level produces a
     new string.
2. **`Action.GetParameter(name, context)`** — pure lookup over Parameters →
   Defaults → NotFound. No resolution side effect.
3. **Source generator** (`PLang.Generators/`) — splits into `Discovery/` +
   `Emission/Action/` + `Emission/Property/{Data,Provider,Legacy}/`.
   Emits per-handler partial class with `ExecuteAsync`, lazy property
   getters, `__SnapshotParams()` for error reporting, and PLNG001 build-time
   diagnostic enforcing Data<T>/[Provider]/[VariableName] only.
4. **`App.Run(action, context)`** (`PLang/App/this.cs:380`) — CallStack
   push, context save/restore, ServiceError wrap; catch filter excludes
   `NRE | OOM | StackOverflow`.
5. **`Variables.Resolve(input, skipInfrastructure)`** —
   `[ThreadStatic] _resolvingVars` for path-bracket cycle protection.

## Threats checked

- [x] Recursive variable resolution (`%a%="%b%"`, `%b%="%a%"`) — guarded
- [x] Expanding-cycle bombs (`%a%="X-%b%"`, `%b%="Y-%a%"`) — depth limit 32
- [x] Source-generator code injection (interpolation of identifiers into
  generated source) — trust boundary is the developer's own code, OK
- [x] JSON deeply nested objects in `UnwrapJsonElement` — `MaxJsonDepth=128`
- [x] `Type.Convert` JSON deserialise has no custom depth — STJ default 64
  protects, OK
- [x] Reflection cache `ResolveMethodCache.GetOrAdd(typeof(T), …)` — T comes
  from compile-time generic, no untrusted source
- [x] `App.Run` catch filter — correctly preserves unrecoverable exceptions
- [x] HashSet leak under depth-trip — root frame's finally nulls
  `_resolvingValues`, no leak across executions

## Findings to write up

1. **`__SnapshotParams` does not honor `[Sensitive]`** — `Emission/Property/
   Data/this.cs:51` and `Emission/Property/Legacy/this.cs:74` capture
   `PrValue` (raw .pr value) and `FinalValue` (post-resolution) verbatim
   into `Error.Params`, which `Errors/Error.cs:215` prints to log/output
   under "📥 Parameters at dispatch:". Same pattern as my standing finding
   for `Variables.Snapshot()`. Currently no handler property carries
   `[Sensitive]`, so realised impact is theoretical — but if a developer
   adds `[Sensitive] public Data<string> ApiKey { get; init; }` to mark a
   secret, they'd reasonably expect the generator to respect it. **Medium**
   (info-disclosure, dormant until first sensitive prop appears).

2. **Dual-purpose: Data emitter `EmitSnapshotEntry` and Legacy emitter
   `EmitSnapshotEntry` both leak — code duplication = security debt.**
   Both must be fixed; per security memory rule, listing both sites.

No criticals or highs found. Branch goes to **PASS** verdict.

## Output

- `security-report.json` (root) — formal findings doc
- `security/v1/summary.md`
- `security/v1/verdict.json` → `pass`
- update `report.json`
