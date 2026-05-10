# architect — modules.code.run/v1

## 2026-05-10 (v1) — Branch carved; plan delivered to test-designer + coder

This branch holds the design and plan for a new PLang action: `code.run`.
A developer types `- run mycode.cs` (or `- run SumList %x%, %y% in mycode.cs, write to %sum%`); PLang reads the file, compiles it, instantiates the entry class, and invokes the named method (or `Start` if none is named) with the supplied positional args. The result flows back through the standard `%__data__%` path.

**Origin.** The design was carved during a session on `runtime2-cleanup` and originally landed there as a single commit (`3c7c827d`). On Ingi's call, that scope-creep was reverted from `runtime2-cleanup` and re-rooted here on its own branch off `a9791ad5`. The session transcript — including what was rejected and why — is preserved at `plan/transcript.md`.

**Key design decisions** (full rationale in `plan.md` + the topic files under `plan/`):

- **`App.Compiler.@this` is a new peer of `App.Code` on `App`.** Compiler privately owns source-read, hash, Roslyn compile, cache, eviction. `App.Code` (the provider registry) is intentionally untouched — mixing the two failed the OBP shape check.
- **`Compiler.Compile()` returns a live `App.Compiler.Runtime.@this`** (not a record). Runtime owns its `AssemblyLoadContext` and entry type, exposes `Start(Context)` / `Invoke(method, args, Context)`, is `IAsyncDisposable`.
- **Action handler is three lines of dispatch.** `Compile`, then `Start` or `Invoke`. No try-catch, no error rewrap; both Compiler and Runtime return typed `Data` shapes already.
- **No special return-mapping syntax.** Method returns `Task` → `Ok(null)`. Returns `Task<T>` → `Ok(unwrapped)`. Standard `, write to %var%` tail handles naming.
- **Sandboxing / signed-script trust — out of scope** for v1. Explicit note in `plan.md`. Needs its own design pass before `code.run` ships outside trusted contexts.

**Plan tree:**

```
.bot/modules.code.run-v1/architect/
├── summary.md                       # this file
├── plan.md                          # spine + stage index + cross-cutting decisions
├── stage-1-runtime.md               # App.Compiler.Runtime.@this  (leaf — build first)
├── stage-2-compiler.md              # App.Compiler.@this + App wiring
├── stage-3-code-run-action.md       # code.run handler + builder Examples + integration cuts
└── plan/
    ├── transcript.md                # the design session, turn-by-turn (read first)
    ├── test-strategy.md             # narrative for test-designer
    └── test-coverage.md             # coverage matrix + failure matrix + new-surfaces inventory
```

**Doc updates landing with this branch:**

- `Documentation/v0.2/writing-modules.md` — new living doc capturing how to write a PLang module. Two living-doc entries logged from the design session (the Path-as-string miss, the ScriptEntry OBP smell).

**Stage status:**

| Stage | File | Status |
|-------|------|--------|
| 1 | [stage-1-runtime](stage-1-runtime.md) | pending |
| 2 | [stage-2-compiler](stage-2-compiler.md) | pending |
| 3 | [stage-3-code-run-action](stage-3-code-run-action.md) | pending |

**Read order for the next bots:**

- **test-designer:** `plan/transcript.md` → `plan.md` → `plan/test-strategy.md` → `plan/test-coverage.md`. Write tests against the matrix, one per row.
- **coder:** `plan/transcript.md` → `plan.md` → stages in order (1 → 2 → 3). Stage 1 is purely additive at the leaf; ships independently.
