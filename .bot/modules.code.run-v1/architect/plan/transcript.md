# `code.run` Design Transcript — 2026-05-09

The design of `code.run` was not handed down — it was sharpened over a
back-and-forth between Ingi and the architect. Test-designer and coder
should read this first. The corrections are where the load-bearing
decisions live; reading only the final plan loses the *why*.

Each turn below is Ingi's prompt verbatim followed by what the architect
landed on after thinking it through. Where the architect was corrected,
that is called out — those corrections are the principle, not a
footnote.

---

## Turn 1 — opening shot

**Ingi:**

> I am finishing up the cleanup, I want to make a module, or rather
> action in module, code.run(Path path, Options?), it would read the
> file in path, compile the code and run it, mathcing with `- run
> file.cs`, with some options, I dont know what options should be.

**Architect:**

Pushed back on two things at once. First: `code` module already exists
as the **provider registry** (`load`/`list`/`remove`/`setDefault` for
ICode-derived providers). Adding `run` next to it would overload the
module name with a second, unrelated lifecycle. Floated `script.run` or
a top-level extension-driven `run` as alternatives.

Second, and more important: "what options should it have" is the wrong
question to start with. Options are the residue of design, not the
seed. The contract has to come first: what is in the .cs file, how
does the script see PLang variables, and what does it return. Caching
should be automatic, not a knob. Sandboxing is policy, not config.

Also flagged that `code.run` is feature work and the branch
(`runtime2-cleanup`) is supposed to be cleanup — not blocking, just
noting.

Ended with the sharpest question: **what does the developer write in
`file.cs`?**

---

## Turn 2 — file shape revealed

**Ingi:**

> it should be code.run. - run (SumList in) mycode.cs
>
>     // mycode.cs file
>
>     public mycode.cs {
>       //run when not method is defined
>       public void Start() {
>         var goal = context.Goal
>       }
>       public int SumList(int x, int y) { ... }
>     }

**Architect:**

Conceded `code.run`. The `code` module covers "things you do with
executable .NET code" — `load` brings a provider DLL in, `run`
executes a script. Different lifecycles, but it's defensible to share
the module.

Read the file shape Ingi pseudo-coded:

- It's a class, not top-level statements. Methods with arguments and
  returns. Allowed to hold state and have a constructor.
- `Start()` is the **default entry** when no method is named.
- `(MethodName in) file.cs` selects a specific method. The `in` is
  the locator.
- `context` is in scope inside the methods.

Things that fall out:

- **Class discovery rule:** "the only public class in the file is the
  entry, fail if there are multiple" — light, no marker attribute
  needed.
- **Context delivery:** if the class has a `(Context)` ctor we pass
  it; if it has `()` we don't. Convention, not magic.
- **Lifetime:** fresh instance per `- run` call. Caching across calls
  is a footgun under concurrent goals.
- **Compile cache:** automatic, hash-keyed, not an option.

Then asked the question that pins down the C# action signature:
**how does `SumList(int x, int y)` get its `x` and `y` from PLang
variables?**

---

## Turn 3 — argument syntax

**Ingi:**

> SumList %x%, %y% in mycode.cs, where %x%, %y% are defined plang
> variables

**Architect:**

Ingi chose a third form the architect hadn't put on the table —
positional, no parens, `in` as the file locator. Reads as English:
"run SumList with x and y in mycode.cs."

Things that fall out cleanly:

- **Default-entry parses without ambiguity:** `- run mycode.cs` (no
  method, no args) goes to `Start()`. Same with `- run in mycode.cs`.
- **Argument types** aren't a new problem — `Data.As<T>(Context)` is
  the existing per-slot coercion machinery.
- **Forbid method overloads** in a script class. PLang has no static
  type inference at the call site, so `SumList(int, int)` and
  `SumList(string, string)` would force the runtime to guess. One
  method per name; if the developer wants two, they use two names.

Drafted the C# action signature:

```csharp
[Action("run", Cacheable = false)]
public partial class run : IContext
{
    public partial Data.@this<string> Path { get; init; }       // ← WRONG
    public partial Data.@this<string>? Method { get; init; }
    public partial List<Data>? Args { get; init; }
}
```

Closed the turn with the return-mapping question.

---

## Turn 4 — the Path correction (pivotal)

**Ingi:**

> can you read up on the doc, you should have picked up that it should
> be Data.@this<Path> Path...; see if you are missing something else,
> I think you should know better. if there is nothing about creating
> modules in plang doc, then we should use this session here are
> learning session, and write down what we have learned, do it
> incramentally, every once in a while improve it., also Args is just
> acceptable, that should also be there.

**Architect (caught the miss, took the lesson):**

The handler should take `Data.@this<FileSystem.Path> Path`, not
`Data.@this<string> Path`. The `Path` type carries
`Absolute`/`Relative`/`Extension`/`FileName`/`Directory`/`MimeType`/
`IsFile`/`Exists`/`Size`/`GoalCall` and validates against the app
root. The auto-wrap is wired by `[PlangType("path")]` plus a static
`Path.Resolve(string, Context)`. Every other path-taking handler
(`file.read`, `file.save`, etc.) uses `Data.@this<FileSystem.Path>` —
the architect read past the canonical example and drafted the wrong
shape.

`Args` as `List<Data>?` was confirmed acceptable.

Ingi also asked for a learning doc. There was no module-authoring
doc. The architect wrote the first version at
**`Documentation/v0.2/writing-modules.md`** as a living document with
a "live additions" tail. The Path miss is logged there as the first
addition.

The corrected signature:

```csharp
[Action("run", Cacheable = false)]
public partial class run : IContext
{
    public partial Data.@this<FileSystem.Path> Path { get; init; }
    public partial Data.@this<string>? Method { get; init; }
    public partial List<Data>? Args { get; init; }
}
```

---

## Turn 5 — show me the body

**Ingi:**

> and the run could would be?

**Architect (got it wrong, flagged for correction next turn):**

Proposed an `App.Scripts` service and showed a Run() body that did
inline Roslyn + cache management + reflection + invocation, returning
a `ScriptEntry` record from a private cache.

This was the wrong shape — see Turn 9.

---

## Turns 6–8 — the compile pipeline + the entry-method-name question

**Ingi (Turn 6):**

> yes, it methods should always public Task<T> SumList. but I would do
> it differently, I read read the .cs file, use a library that
> compiles into .dll, then import the dll, we can do that in c#
> app.code.add({name}.dll")

**Ingi (Turn 7):**

> run()

**Ingi (Turn 8):**

> or start()

**Architect:**

Two design moves locked here:

1. **All script methods are `public Task` or `public Task<T>`.** No
   sync, no overloads. Reject sync methods at compile-time analysis
   (walk the SyntaxTree before handing to Roslyn — cheaper failure,
   better error message).

2. **Default entry method is `Start()`.** Architect ruled `Run()` out
   because it visually collides with the action handler convention
   (every action handler implements `public Task<Data.@this> Run()`),
   which would make a script class read like a half-built handler.
   `Start` echoes PLang's `Start.goal` convention — same intuition
   carried into script classes. Strict — if a script defines `Run` and
   no `Start`, fail with a clear error. Don't accept either.

Then proposed the compile-then-load architecture: read `.cs`, compile
to a DLL, load via the same path `code.load` uses. Surfaced the real
friction — `code.load` filters for `ICode`-implementing types, so a
plain `MyCode { Task Start(); Task<int> SumList(int, int); }` doesn't
qualify and gets silently skipped. Floated three bridges:

1. Relax the filter (loses ICode type-safety in the registry).
2. Sister table on `App.Code` for scripts.
3. Synthesize an ICode wrapper at compile time.

Architect leaned (2) — a parallel `_scripts` dictionary on `App.Code`,
keyed by absolute path, with `ScriptEntry` records.

This was the second wrong shape — see Turn 9.

---

## Turn 9 — the OBP correction (pivotal)

**Ingi:**

> no, scriptentry, think obp. c#: `var content = file.read({path});`
> `var compiler = new SomeNugetCompiler();` `var runtime =
> compiler.compile();` `runtime.start();` then if start and compile
> doesn't exists this clenaly we make this cleanly,

**Architect (caught the smell):**

The `ScriptEntry` record + `_scripts` table on `App.Code` +
orchestration `RunScript` method was the OBP shape smell from the
checklist, in the wild, by the architect. Three smells in one design:

- **`ScriptEntry`** — a record holding state (hash, ALC, type, ctor
  flag) that some other class then reaches into. Data here, behavior
  there.
- **`_scripts` side-table on `App.Code`** — same logical thing
  (script lifecycle) glued onto a class whose actual job is the
  provider registry. Cross-purpose ownership.
- **`RunScript` method** — orchestration that reads from the table,
  does the reflection, does the invocation. Allocate-here /
  mutate-there / clean-up-elsewhere across files.

The clean shape: two `@this` classes, each owning their state and
their work.

```
PLang/App/Compiler/
├── this.cs              ← App.Compiler.@this
└── Runtime/
    └── this.cs          ← App.Compiler.Runtime.@this
```

`Compiler` privately owns: source-read, hash, the Roslyn compile, the
cache, eviction. The cache is its private state, not exposed.

`Runtime` is what `Compile()` returns. Privately owns the
`AssemblyLoadContext`, the entry type, the constructor probe.
Exposes `Start(Context)` and `Invoke(string method, args, Context)`.
Is `IAsyncDisposable` — unloads its ALC on disposal.

The action handler reads exactly like Ingi's pseudocode:

```csharp
public async Task<Data.@this> Run()
{
    var runtime = await Context.App.Compiler.Compile(Path.Value);
    return Method?.Value is null
        ? await runtime.Start(Context)
        : await runtime.Invoke(Method.Value, Args ?? new(), Context);
}
```

Errors are typed by the class that detects them: `Compiler` raises
`CompileFailed` / `NoEntryClass`; `Runtime` raises `MethodNotFound` /
`MustReturnTask` / `ArityMismatch`.

`App.Code` is **untouched**. No new tables, no ICode filter
relaxation. `Compiler` is its own peer of `Code` on `App`.

The Path miss and the ScriptEntry smell were both logged as live
additions to `Documentation/v0.2/writing-modules.md`.

---

## Turn 10 — return mapping closes itself

**Ingi:**

> do you worry about return mapping, you return Task or Task<T> and
> dont care any more

**Architect:**

Architect was inventing a question. `code.run`'s handler returns
`Task<Data.@this>` whose `Value` is the unwrapped `Task<T>` result.
The runtime drops it in `%__data__%`. If the developer wants it
named, they chain `, write to %sum%` — the standard form every
action uses. There is no special return syntax for `code.run`. It is
an action like any other.

The example already encoded this answer:
`code.run ... | variable.set Name([string] %sum%), Value([object] %__data__%)`.

---

## Final settled design (recap)

- **Action:** `code.run`
- **Syntax:** `- run (Method)? %args%, ... in <file>.cs (, write to %var%)?`
- **Handler params:**
  - `Data.@this<FileSystem.Path> Path` (required)
  - `Data.@this<string>? Method` (optional, null → `Start`)
  - `List<Data>? Args` (positional, coerced via `Data.As<T>(Context)`)
- **Script file rule:**
  - One public class per file. Multiple = error.
  - Methods are `public Task` or `public Task<T>` only. Sync = compile-time error.
  - No method overloads. Two methods with the same name = compile-time error.
  - Default entry is `Start`. Optional ctor signature is `()` or `(Context)`.
- **Compile pipeline:**
  - Handler → `App.Compiler.Compile(Path) → Runtime`
  - `Runtime.Start(Context)` or `Runtime.Invoke(method, args, Context)`
  - Roslyn (`Microsoft.CodeAnalysis.CSharp`) is the compile backend.
- **OBP shape:**
  - `App.Compiler.@this` privately owns source-read, hash, compile, cache, eviction.
  - `App.Compiler.Runtime.@this` privately owns `AssemblyLoadContext`, entry type, ctor probe.
  - `App.Code` is untouched.
- **Errors:** typed at the class that detects them.
  `Compiler`: `CompileFailed`, `NoEntryClass`.
  `Runtime`: `MethodNotFound`, `MustReturnTask`, `ArityMismatch`.
- **Return mapping:** standard `Task<Data.@this>` → `%__data__%` →
  optional `, write to %var%`. No special syntax.
- **Out of scope (separate design pass before ship):** sandboxing,
  reference allowlist, signed-script trust model.
- **Branch question:** the cleanup branch is closed; this is feature
  work and ideally lives on its own branch off `runtime2`.

## What test-designer and coder should take from this

Two principles got reinforced this session, beyond the specific
design:

1. **`Data.@this<FileSystem.Path>` is the only correct shape for path
   parameters.** `string` discards every property the `Path` type
   carries. There are other `[PlangType(...)]` domain classes; prefer
   them over primitives.

2. **OBP applies to internal services, not just modules.** When a
   handler's owner needs cached/derived state, the live object owns it
   — don't introduce records-as-state-bags and tables-on-other-classes
   to hold what should be a single `@this` with private fields. If
   the data has behavior attached, the data should be a class.

Both are now logged at `Documentation/v0.2/writing-modules.md` as
living-doc entries.
