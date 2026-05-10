# Stage 3: `code.run` action handler

**Goal:** Wire the `code.run` action to `Compiler` + `Runtime`. Three lines of dispatch in the handler; the heavy lifting was done in Stages 1 and 2.

**Scope:**
- New action handler: `PLang/App/modules/code/run.cs`.
- LLM `[Example]` registration so the builder maps `- run X %a%, %b% in foo.cs` to `code.run` with the right parameter shape.
- End-to-end goal tests (the integration cuts) and developer-facing failure tests.

**Excluded:**
- Anything about Compiler/Runtime internals — Stages 1 and 2 closed those.
- Sandboxing / signed-script verification — separate design pass.

**Deliverables:**
- `PLang/App/modules/code/run.cs` — the handler.
- Goal test files under `Tests/code-run/` (or wherever the existing `Tests/` tree organises module tests by name; mirror the `Tests/file/` shape).
- C# tests for handler-shape behaviors (rows 3.1–3.4 in [plan/test-coverage.md](plan/test-coverage.md)).

**Dependencies:** Stages 1 and 2.

## Design

The handler in full (no sketch needed — three lines):

```csharp
using App.Variables;

namespace App.modules.code;

[System.ComponentModel.Description(
    "Compile and run a method in a .cs file. Method name optional " +
    "(defaults to Start). Positional args bind by parameter order " +
    "with type coercion via Data.As<T>.")]
[Example("run mycode.cs",
    "code.run Path([path] mycode.cs)")]
[Example("run SumList %x%, %y% in mycode.cs, write to %sum%",
    "code.run Path([path] mycode.cs), Method([string] SumList), " +
    "Args([list] %x%, %y%) | variable.set Name([string] %sum%), " +
    "Value([object] %__data__%)")]
[Action("run", Cacheable = false)]
public partial class run : IContext
{
    public partial Data.@this<FileSystem.Path> Path { get; init; }
    public partial Data.@this<string>? Method { get; init; }
    public partial List<Data.@this>? Args { get; init; }

    public async Task<Data.@this> Run()
    {
        var compileResult = await Context.App.Compiler.Compile(Path.Value);
        if (!compileResult.Success) return compileResult;
        var runtime = compileResult.Value!;

        return Method?.Value is null
            ? await runtime.Start(Context)
            : await runtime.Invoke(Method.Value, Args ?? new(), Context);
    }
}
```

### Why these decisions

**Why two `[Example]` entries.** The LLM needs to see both the default-entry shape (`- run mycode.cs`) and the named-method-with- args shape (`- run SumList %x%, %y% in mycode.cs, write to %sum%`). The two parses are structurally different — one has no `Method` / `Args`, the other has both — and a single example would bias the LLM toward whichever shape was given. Two examples teach the disjunction.

**Why `Cacheable = false`.** A script's effects depend on whatever the script does — file I/O, state mutation, time. The runtime's `[Action]` cache memoises by (action + parameter signature), which would silently skip re-execution. `code.run` is fundamentally side-effecting; never cache it.

**Why the handler doesn't try-catch.** Both `Compile` and `Start`/`Invoke` return typed `Data.@this` results — failures are already packaged with error codes. Adding a try-catch in the handler would either swallow them (wrong) or rewrap them as a different error shape (wrong). Stage 2's error factory is the source of truth for compile errors; Stage 1's is the source of truth for runtime errors; the handler trusts both.

**Why `Args ?? new()` instead of guarding for null upstream.** PLang's parameter conventions: when the developer writes a step with no args, the generator emits a null `Args`. The handler handles the empty case trivially with `?? new()`. No need to enforce non-null in the generator's PLNG001 gate.

**Why `Path.Value` is unwrapped at the handler boundary.** Standard shape — see `file/read.cs:24` for the canonical `var path = Path.Value;` pattern. The handler is the last-string-typed boundary before crossing into the typed work; the unwrap happens here, once, and the typed `FileSystem.Path` flows inward.

### LLM example check

The Example chain expressed as the builder will see it:

```
input:  - run SumList %x%, %y% in mycode.cs, write to %sum%
chain:  code.run Path([path] mycode.cs), Method([string] SumList), Args([list] %x%, %y%)
        | variable.set Name([string] %sum%), Value([object] %__data__%)
```

The `[type]` prefixes hint typed args. `Args([list] %x%, %y%)` carries the variadic `List<Data>` shape. `variable.set Name([string] %sum%), Value([object] %__data__%)` is the standard return-mapping tail — appears in `file/read.cs`'s example and many others. No new conventions invented.

For default-entry:

```
input:  - run mycode.cs
chain:  code.run Path([path] mycode.cs)
```

If the developer wrote `- run mycode.cs, write to %answer%`, the chain extends with `| variable.set Name([string] %answer%), Value([object] %__data__%)` automatically — that's the existing write-to mechanism, not anything `code.run` needs to express.

### Goal tests (integration cuts I.A, I.B, I.C)

Mirror `Tests/file/` for placement. Each cut is one `.test.goal` plus its supporting `mycode.cs`:

```
Tests/code/
├── run-default-entry.test.goal      # cut I.A
├── run-named-method-args.test.goal  # cut I.B
├── run-recompile-on-change.test.goal# cut I.C
├── run-failures.test.goal           # rows 4.1–4.5
└── scripts/
    └── mycode.cs                    # the script under test
```

Cut I.A (`run-default-entry.test.goal`):

```
Start
- run scripts/mycode.cs, write to %answer%
- assert %answer% equals 42
```

with `mycode.cs`:

```csharp
public class MyCode {
    public async Task<int> Start() => 42;
}
```

Cut I.B (`run-named-method-args.test.goal`):

```
Start
- set %x% = 5
- set %y% = 7
- run SumList %x%, %y% in scripts/mycode.cs, write to %sum%
- assert %sum% equals 12
```

with `mycode.cs` extended:

```csharp
public class MyCode {
    public async Task<int> Start() => 42;
    public async Task<int> SumList(int x, int y) => x + y;
}
```

Cut I.C (`run-recompile-on-change.test.goal`): two-step test that runs once, rewrites the source, runs again, asserts the second result differs. The mechanics need a small file-write helper in the test goal — coordinate with test-designer on whether `file.save` is viable inside a `.test.goal`, or whether this cut belongs in C# instead with a real-filesystem fixture.

### Failure tests (rows 4.1–4.5)

`Tests/code/run-failures.test.goal`. One step per failure shape, each asserting the error code. Pattern (mirrors negative-path tests elsewhere):

```
Start
- run nonexistent.cs
- assert error code is "FileNotFound"
```

Repeat for `MethodNotFound`, `ArityMismatch`, `MethodNotFound("Start")` (when no Start), `MustReturnTask`. Each goal step's assertion lives in the same `.test.goal` if the test runner supports it, otherwise split across files.

### C# tests for handler shape (rows 3.1–3.4)

`PLang.Tests/App/modules/code/RunTests/`:

- `MissingPathParameterTests.cs` — handler invoked without `Path` → `MissingRequiredParameter` (generator-emitted). Uses the standard pattern from `PLang.Tests/Generator/PLNG001Tests.cs` or wherever the missing-param tests already live; mirror that.
- `DispatchTests.cs` — handler with `Method` null routes to `Runtime.Start`; with `Method` set routes to `Runtime.Invoke`. Stub `Compiler` to return a hand-built `Runtime`; assert which method was called with which args.
- `ErrorPassthroughTests.cs` — when `Compiler.Compile` returns a `CompileError`, the handler returns that same `Data` shape unchanged (no double-wrap).

### Files

```
PLang/
└── App/
    └── modules/
        └── code/
            └── run.cs                          NEW

Tests/
└── code/
    ├── run-default-entry.test.goal             NEW   (cut I.A)
    ├── run-named-method-args.test.goal         NEW   (cut I.B)
    ├── run-recompile-on-change.test.goal       NEW   (cut I.C — see note)
    ├── run-failures.test.goal                  NEW   (rows 4.1–4.5)
    └── scripts/
        └── mycode.cs                           NEW

PLang.Tests/
└── App/
    └── modules/
        └── code/
            └── RunTests/
                ├── MissingPathParameterTests.cs NEW  (row 3.1)
                ├── DispatchTests.cs             NEW  (rows 3.2–3.3)
                └── ErrorPassthroughTests.cs     NEW  (row 3.4)
```

### What the coder should NOT do

- Do not extend `App.Code` with anything. The two systems are parallel; mixing them is the OBP smell the design specifically rejected.
- Do not add try-catches in the handler that re-package errors. Stages 1 and 2 own the error packaging.
- Do not introduce a `ScriptEntry`-shaped record anywhere. If you feel the need for one, the design has drifted — re-read [plan/transcript.md](plan/transcript.md), turn 9.
- Do not pass `string` paths anywhere except as the LLM-side input to `Path.Resolve`. Internal contracts use `FileSystem.Path`.
