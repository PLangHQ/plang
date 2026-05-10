# `code.run` — Test Strategy

A narrative for test-designer. Read [../plan.md](../plan.md) first to
absorb the architecture, then [transcript.md](transcript.md) for the
design history. This file tells you how to *partition the testing
work*; [test-coverage.md](test-coverage.md) is the matrix you write
tests against.

## Scope

The integration cuts (below) are the contract for end-to-end behavior:
"a developer types `- run X %a%, %b% in foo.cs` and gets a typed value
back, or a typed failure." Per-`@this` C# tests sit beneath them and
pin the internal contracts of `Compiler` and `Runtime` directly. They
are not redundant — the integration cuts prove the path is wired,
the unit tests prove each `@this` is reachable in negative-path shapes
the integration cuts can't enumerate cheaply.

## Test layer mapping

Three layers, each with a clear job:

- **C# TUnit (`PLang.Tests`).** Owns the internal contract of
  `App.Compiler.@this` and `App.Compiler.Runtime.@this`. Tests
  instantiate these directly and exercise their methods. This is
  where compile-cache hits/misses, eviction, error packaging, ctor
  probing, method dispatch, and arg coercion get pinned.

- **PLang `.goal` tests (`Tests/`).** Owns the developer-facing
  surface — the `- run` syntax, the variable bindings, the
  `, write to %sum%` chaining, the result flowing through
  `%__data__%`. One `.goal` test per behavior the developer can
  observe.

- **Integration cuts.** End-to-end through the `code.run` action
  itself: builder parses `- run X in foo.cs` → handler invokes
  Compiler+Runtime → result lands in PLang variables → next step
  consumes it. Done as PLang tests with a real `.cs` file on disk
  and a real Compiler.

The rule: **C# tests pin internal `@this` behavior; goal tests pin
developer-facing surfaces; integration cuts prove the wiring.** The
matrix in `test-coverage.md` assigns each behavior to a layer.

## Integration cuts

Three cuts, each producing a working end-to-end .goal test that
test-designer must be able to defend as covering one specific
contract.

### Cut A — Default entry, scalar return

`mycode.cs` has `public async Task<int> Start() => 42;`. PLang
script:

```
- run mycode.cs, write to %answer%
- write %answer%
```

What it must prove: handler accepts no `Method`, dispatches to
`Start`, the `int` flows through `Task<T>` unwrap into `Data.@this`,
the value lands in `%answer%`, the next step reads it as an integer.

### Cut B — Named method, multiple positional args, type coercion

`mycode.cs` has
`public async Task<int> SumList(int x, int y) => x + y;`. PLang
script:

```
- set %x% = 5
- set %y% = 7
- run SumList %x%, %y% in mycode.cs, write to %sum%
- assert %sum% equals 12
```

What it must prove: positional args bind by parameter order, scalar
type coercion via `Data.As<int>(Context)` works for PLang values
(which may arrive as strings), result flows through normally.

### Cut C — Recompile on change

`mycode.cs` has `Start() => 1`. After the first `- run`, the test
overwrites `mycode.cs` with `Start() => 2` and runs again. Result
must change from `1` to `2`.

What it must prove: the Compiler's path→hash invalidation actually
fires, the new content compiles into a new Runtime, the old one is
unrooted, and the second invocation observes the new behavior.

## What is NOT covered by the cuts

The cuts cover the green path of the integration. Everything else is
handled below them in the matrix:

- **Compiler internals:** cache hits (no recompile when source
  unchanged), per-class shape rules (multiple public classes,
  non-public class, no class), per-method shape rules (sync method,
  void return type, generic method, overloaded methods), ctor probe
  (`()` vs `(Context)` vs unsupported), reference resolution against
  loaded assemblies. All C# tests against `Compiler.@this` directly.

- **Runtime internals:** `Start` missing, `Invoke` with unknown
  method name, arity mismatch (too few, too many), invocation
  exception (script throws), `DisposeAsync` actually unloads the
  ALC. All C# tests against `Runtime.@this` directly with a
  pre-cooked assembly.

- **Failure surfaces in PLang:** developer types `- run nonexistent.cs`
  → file-not-found packaged into `Data.Fail`. Developer types
  `- run BogusMethod %x% in mycode.cs` and the method doesn't exist
  → `MethodNotFound`. Developer types `- run SumList %x% in mycode.cs`
  with one arg when SumList takes two → `ArityMismatch`. PLang tests,
  one per failure shape, asserting the error code surfaces.

- **Action surface:** the `code.run` handler's missing-`Path`
  parameter guard. C# test against the handler with a Data parameter
  bag missing `Path`.

These are enumerated as rows in `test-coverage.md`.

## Why goal tests at all (vs. only C# tests)

Two reasons. First, the LLM/builder side of `- run X %a%, %b% in
foo.cs` is part of the contract — if the builder mis-parses the
syntax into the wrong `Method`/`Args` shape, the handler is correct
but the developer experience is broken. Goal tests catch that. Second,
the variable-bag wiring (`%__data__%` → `, write to %sum%`) only
exercises end-to-end. C# tests can't fake that without rebuilding the
runtime around them.

## Why C# tests at all (vs. only goal tests)

Negative paths are cheap in C# and expensive in PLang. Asserting
"`Compiler.Compile` returns `CompileError.MultipleEntryClasses` when
the source has two public classes" is six lines of C# and would be
twenty lines of `.goal` plumbing per shape. The C# layer is also the
only place that can directly observe ALC unloading and cache state.
