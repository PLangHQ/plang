# PLang Project

## PLang Syntax (v0.1 builder limitations)
- Cannot combine two modules in one step (e.g., `if + set` must be separate steps)
- foreach always calls a goal, does not support sub steps. Syntax: `foreach %products%, call DoProduct item=%product%`, `item=%variableName%` not `%variableName%=%item%`
- Simple set statements work: `set %step.Name% = %stepResult.method%`

## Runtime2 Conventions
- **`app/` is lowercase** for PLang vocabulary (`actor`, `goals`, `variables`, `channels`, `errors`, `events`, `filesystem`, `formats`, `keepalive`, `snapshot`, `tester`, `types`, `config`, `callstack`, `data`) and PascalCase for C# infrastructure (`Attributes`, `Diagnostics`, `Services`, `Statics`, `Utils`). Seven engine concepts (`Cache`, `Builder`, `Callback`, `Settings`, `Modules`, `Code`, `Debug`) merged with their action-module counterparts under `app/modules/<name>/` — no separate top-level folder remains for those. **Property names on `app.@this` stay PascalCase** (`.Cache`, `.Builder`, `.Code`, `.Modules`, `.FileSystem`, `.Goals`, etc.) — only the *types* live in lowercase namespaces. So `ctx.App.FileSystem.Read(...)` is property access (stays capital); `app.filesystem.@this` is the type. **One keyword carve-out**: `app/filesystem/Default/` stays PascalCase because `default` is a C# keyword. Two PLang action renames fell out of the rename: `app.run` → `environment.run`, `builder.app` → `builder.load` (both temporary names, deliberate naming pass deferred).
- **`@this` convention**: Every folder's primary class is `@this` in `this.cs`. Consumers use global aliases (e.g., `global using Step = ...Step.@this;`). Within parent namespaces, use `ChildNamespace.@this`.
- **Goal properties**: use `Path` and `PrPath` (relative), not `FilePath`/`PrFilePath`/`RelativePath`
- **Step.Goal**: has `[JsonIgnore]` to avoid circular reference in serialization
- **v0.2 .pr.json format**: single file with all steps
- **Lazy params**: Source generator emits a `partial class` extension on the action record itself (no separate `*__Generated` record) — properties resolve `%var%` lazily on first access via `Action.GetParameter(name).As<T>(Context)`
- **Handler naming**: records = action name (`set`, `save`), handlers = `SetHandler`, `SaveHandler` (partial)
- **`ICodeGenerated`**: added automatically by the source generator — handlers never implement it directly
- **`Data`**: universal result type with `Value`, `Properties`, `Error`, `Success`, `Ok()`, `Fail()`, `Merge()`. Extended via Properties.
- **`Action.Return`**: `List<Data>?` — simple list of return variable mappings, no wrapper class
- **Action `Run()` returns are typed via the signature.** `Task<Data<T>>` for concrete T (catalog: `→ returns T`), `Task<Data<object>>` for genuinely polymorphic returns (catalog: `→ returns data`), bare `Task<Data>` only for actions that produce no value (no `→ returns` line; compile LLM rejects trailing `write to %x%`). `Modules.Describe()` reads the signature; `action.@this.ReturnTypeName` carries T's PLang name; no separate `Type=` parameter — the `Data<T>` wrapper carries it. **Footgun:** `data.@this<T>`'s implicit operator `@this<T>(T value)` silently double-wraps when `T = object` and the source is itself a `Data` (`Data<object>{ Value = Data<bool>{...} }`). For polymorphic forwarders (`goal.call`, `llm.query`, condition evaluators) that return a `Data` produced elsewhere, stay on bare `Task<Data>`; for owned-construction actions, use `data.@this<object>.Ok(value)` — never `return innerDataInstance;`. Full rule + migration status: `Documentation/v0.2/good_to_know.md` "Action `Run()` returns are typed — and the `Data<T>` implicit-operator footgun".
- **Truthiness — `IBooleanResolvable`.** A value's boolean meaning belongs to the value, not to `Data`. `Data.ToBoolean()` is the sync fallback (null/false/0/"" falsy); do **not** add type-specific cases. A type that knows its own truthiness implements `app.data.IBooleanResolvable` (`Task<bool> AsBooleanAsync()`) — `path` does, where truthiness means "does the resource exist" (stat for `FilePath`, HEAD for `HttpPath`). Because the probe can be I/O, the condition-evaluation pipeline is **async**: `IEvaluator.Evaluate` returns `Task<data.@this>`, `Operator.Evaluate` is `Func<data.@this?, data.@this?, Task<bool>>`, `assert.IsTrue`/`IsFalse` are async. A new operator or evaluator must `await`. Full rule: `Documentation/v0.2/good_to_know.md` "Truthiness — `IBooleanResolvable` and async condition evaluation".
- **Action prose lives in markdown, not attributes.** Class shape (parameters, types, modifier role, defaults) goes in C# attributes on the action handler. **Prose** (Description, Notes, Examples) goes in `os/system/modules/<module>/{module,<action>}.{description,notes,examples}.md`. `[Description]`/`[ModuleDescription]`/`[Example]` no longer exist on action handlers — don't add them back. Per-action Notes render in the user message of each Compile call **only when the planner picked that action**; `Compile.llm` keeps only the cross-cutting kernel. `module.*.md` is a reserved stem (module-wide teaching layer); the renderer concats module-first + blank line + action. Orphan files surface as warnings via `MarkdownTeaching.ScanOrphans`. Full guide: `Documentation/v0.2/action-catalog.md`; loader: `PLang/app/modules/MarkdownTeaching.cs`.
- **No `System.IO.*` reaches in production C#.** Filesystem access must go through `app.types.path.@this` verbs (`ReadText`, `WriteText`, `List`, `Stat`, `ReadBytes`, `ExistsAsync`, `MoveTo`, `CopyTo`, …) — every verb passes through `FilePath.AuthGate(verb)`, the only thing stopping out-of-root reads/writes. `System.IO.File`, `System.IO.Directory`, `System.IO.FileInfo`, and `System.IO.Path.Combine`/`GetDirectoryName`/`GetFullPath`/… are banned. A handler reaching there bypasses the actor's permission model. Filesystem paths live as `path.@this` in interior C#; `string` only appears at the perimeter (CLI args, JSON-on-disk shape, the App-anchor strings `App.AbsolutePath`/`OsDirectory`/`OsAbsolutePath`). The crossing rule: `path.Resolve(rawString, context)` once at the perimeter. Build-time gate: PLNG002 at **error** severity — PLang and PlangConsole build clean with zero PLNG002 warnings as of the `purge-systemio-from-actions` merge, and any regression fails compilation. Exempt: `app.types.path.**` (verb surface uses System.IO post-AuthGate), `Path.DirectorySeparatorChar`/`AltDirectorySeparatorChar`/`PathSeparator`/`VolumeSeparatorChar` (constants, not IO). `.Absolute` discipline: any reach for `.Absolute` outside `app.types.path.**` MUST `await path.Authorize(verb)` first — verbs do this automatically; manual Authorize only for take-over APIs that need the raw string (sqlite, `Assembly.LoadFrom`). Full rule + migration status: `Documentation/v0.2/good_to_know.md` "System.IO Is Banned in Production C#".
- **Data is not enveloped.** Data IS the wire shape — `{name, type, value, properties, signature}`. Do not introduce parallel wrapper types ("Envelope", "Wire", "Wrapper") for Data's serialization shape; the wire shape is Data's own shape with `[Out]` filtering. If you find yourself building a parallel type to bypass `[JsonIgnore]`, the right answer is to add an `[Out]`-aware filter and let `app.data.Wire` (renamed from `WireJsonConverter` on `data-normalize`) handle the wire layout. The historical `Envelope` class on `plang/Data.cs` (deleted on `data-serialize-cleanup`) is the load-bearing example of the smell. The single wire serializer is `application/plang` (the `+data` variant has been merged in); signing fires sign-if-missing inside `Wire.Write` — owners do not call `EnsureSigned()` at egress boundaries. The value slot is built via `data.Normalize(View) → IWriter`, so new domain types ship by adding `[Out]` to the properties that should cross the wire — **do not** add a `JsonConverter` to a new domain type. Full background: `Documentation/Runtime2/data-spec.md` §15a + §16 + §16a, `Documentation/v0.2/callbacks.md` "Sign-if-missing — the converter does it", `Documentation/v0.2/good_to_know.md` "Domain types ride the wire as property bags".
- **No `Console.*` writes in production C#.** Channels exist to make I/O redirectable; `Console.WriteLine`/`Console.Error.WriteLine` bypass that. Diagnostics → `await context.App.Debug.Write(...)` (debug channel, gated on `--debug`). User-facing chatter → `await app.CurrentActor.Channels.WriteTextAsync(global::app.channels.@this.Output, ...)` (do **not** route through `Debug.Write` — its `IsEnabled` gate would silence it without `--debug`). Interactive prompts use a two-call pattern across the split `output`/`input` pair (write via `output`, read via `StreamReader(input.Stream, leaveOpen: true)`). Permitted exceptions: `Console.IsInputRedirected`/`IsOutputRedirected` (queries, not writes) and `PlangConsole/Program.cs:26` (process-boundary last resort if channels failed to wire). Full rule + test-fixture pattern: `Documentation/v0.2/good_to_know.md` "Console.* Is Banned in Production C#".

## OBP Shape Smells (audit before writing or reviewing C#)

When reading or writing C#, run this checklist. Each item is a yes/no question; any "yes" means the shape is wrong and the fix is structural, not a line edit.

1. **Public mutable collection with rules enforced from outside.** A type exposes `public List<T>` / `Dictionary<K,V>` / `HashSet<T>` and the `Add` / `Remove` / locking / eviction lives in another file. The collection should become its own `@this` type with private lock and `Add(...)` / `IReadOnlyList<T>` surface.
2. **Cross-file lock target.** `lock (other.X)` taken from outside `other`'s class — the type that owns the data isn't the type that owns the discipline.
3. **Same logical thing stored twice across types** (overlapping semantics, similar names, same element type, same role).
4. **Allocate-here / mutate-there / clean-up-elsewhere.** One collection's lifecycle split across three files.
5. **Producer hands back raw; consumers transform identically.** A property is exposed in one shape and most callers immediately apply the same operation to make it usable — `obj.Path + "/"`, `obj.Path.TrimStart('/')`, `obj.Name.ToLowerInvariant()`, `Path.GetDirectoryName(obj.Path)`, `obj.Url.Trim().TrimEnd('/')`. Every fix to that transform now has N call sites; one consumer forgetting it produces a subtle divergence bug. The discipline (separator, case, trimming, parent-derivation, whatever it is) belongs on the owner: rename the existing property or add a sibling that returns the form callers actually use (`Goal.RelativePath` instead of every site calling `.Path.TrimStart('/')`; `File.DirectoryName` instead of every site doing `LastIndexOfAny`). Grep for the literal transform on the property name — `\.{PropertyName}\.(TrimStart|TrimEnd|ToLower|ToUpper|Replace|GetDirectoryName|Substring|Split)` — three or more hits means the property is shaped wrong. Trivial single-char appends (`+ "/"`) count too.
6. **Holds a reference AND a flat copy of properties reachable through it.** A class declares `Foo Foo { get; }` (or `Foo? Foo`) and *also* scalar fields whose values are all reachable as `Foo.X`, `Foo.Y`, `Foo.Z`. The flat copy costs more memory than the 8-byte reference and creates two views of the same data that can silently drift: when the underlying `Foo` is rebuilt or mutated, the flat fields stale because no one updates them. Construction sites also double-pay — every place that builds the outer class has to remember to populate both the reference and every flat field, and forgetting one is a subtle bug that compiles. Detection: read every scalar property on a class that has a reference field, and ask *"is this `Foo.X`?"* If yes for three or more fields, the flat fields are the smell. Fix by deleting the flat fields and routing consumers through the reference (`file.Goal?.Path` instead of `file.Path`). When the outer class needs to survive `Foo` being null (the .pr is missing, the discovery failed) keep a *single* "summary" field that captures only what's needed for the failure path — never a parallel mirror of everything `Foo` exposes.
7. **Courier reaches into `Data.Value`.** A relay layer (a handler that forwards Data, variable memory, callstack, channel routing, signing, the wire envelope) does `data.Value as X` or `if (data.Value is X)` to branch on the contained value. The code is opening a package that should stay closed mid-flight. Only leaf actions (handlers declaring a typed `Data<T>` parameter) and leaf serializers (the value's own per-(type, format) renderer file) get to read `.Value`. Detection: grep for `\.Value (is|as|switch)` outside files that declare `Data<T>` parameters. Full rule: `Documentation/v0.2/object_pattern_formal.md` Rule #9 — "Only leaves touch `Data.Value`".

If removing one line of choreography requires editing three files, those three files are one missing type.

Full checklist and worked example: `Documentation/v0.2/good_to_know.md` "OBP Smell Checklist".

## Source Generator
- PLang.Generators: netstandard2.0, IIncrementalGenerator
- OBP shape: entry `PLang.Generators/this.cs` → `Discovery/this.cs` (Roslyn boundary) + `Emission/Action/this.cs` (per-handler) + `Emission/Property/{Data,Code}/this.cs` (polymorphic per-property)
- Filter out `EqualityContract` (protected, not public) when scanning virtual props
- Generated records must be `public sealed record` to match base access level
- In tests: use `System.Type?` (not `Type?`) to avoid ambiguity with `PLang.Runtime2.Memory.Type`
- **Property kinds (PLNG001 build-time gate)**: action handler properties must be `Data<T>` (or plain `Data`) or `[Code] T`. Anything else fails the build with `PLNG001`. For parameters that *name* a variable (write targets, read-by-name lookups: `variable.set`, `list.*`, `loop.foreach` ItemName/KeyName), use `Data<app.variables.Variable>`. `Variable` implements `IRawNameResolvable`, which tells `Data.As<T>` to skip its `%var%` substitution branch and dispatch to `Variable.Resolve(raw, ctx)` directly — both `value="%x%"` and bare `value="x"` collapse to `Variable { Name = "x" }`. Use sites read `Foo.Value` (Variable's implicit `string` operator covers method-call boundaries; `ToString() => Name` makes interpolation read naturally). Non-nullable `Data<Variable>` slots get a generator-emitted pre-Run guard that surfaces `MissingRequiredParameter` (auto-detected via the `IRawNameResolvable` marker through Discovery → ActionClassInfo → Action emitter, mirroring `[IsNotNull]`).
- **Incremental cache**: `ActionClassInfo` is a record with `EquatableArray<T>` collections (no `IPropertySymbol` references) so Roslyn cache hits on semantically identical inputs. Tracking-name constants on `PLang.Generators.@this` exist for `IncrementalCacheTests`.
- **Test alias clash**: `PLang.Tests/GlobalUsings.cs` aliases `Data` and `Variables` to types. Do NOT create `PLang.Tests.App.Data` or `PLang.Tests.App.Variables` namespaces — they shadow the alias for all sibling test files (CS0118). Convention: use `*Tests` suffix on folder/namespace when mirroring `PLang/app/data/` etc. → `PLang.Tests/App/DataTests/`, `PLang.Tests/App/VariablesTests/`. (Test folder names under `PLang.Tests/App/` stay PascalCase — only the source paths under `PLang/app/` are lowercase.)

## Key Files
- PlangConsole is the executable project (not PLang which is a library)
- system/builder/*.goal — the PLang builder written in PLang
- PLang/Runtime2/Engine/this.cs — Engine root (@this, IAsyncDisposable)
- PLang/Runtime2/Engine/Goals/Goal/this.cs — Goal entity (@this)
- PLang/Runtime2/actions/*.cs — action handlers (variable/set, file/read, output/write, etc.)
- PLang/Runtime2/actions/IClass.cs, ICodeGenerated.cs — handler interfaces
- PLang/Runtime2/Engine/Memory/Data.cs — universal data container + Type class
- PLang/Runtime2/Engine/Utility/TypeMapping.cs — PLang type names + MIME types → CLR types
- PLang/Runtime2/Engine/Utility/GoalMapper.cs — maps Building.Model → Runtime2
- PLang/Runtime2/GlobalUsings.cs — global type aliases for @this classes
- PLang.Generators/this.cs — source generator entry point (`Discovery/`, `Emission/Action/`, `Emission/Property/{Data,Code}/` underneath)
- For full OBP details: `Documentation/Runtime2/plang_object_based_pattern.md`

## Build
- Always run `plang build` without specifying a goal name — it builds everything
- NEVER delete .build folders
- Use `PlangConsole/bin/Debug/net10.0/plang.exe` for net10.0 builds
- Don't use Select-String in bash — it doesn't work

## Running plang Tests

- All plang tests live under `Tests/` (uppercase). Never under `tests/`, `.bot/`, `.build/`, `os/`, or any other tree.
- When running `plang --test`, change directory into `Tests/` first so discovery is bounded to the canonical location:

  ```bash
  cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test
  ```

  Running `plang --test` from the project root will surface stale `.test.goal` files under `.bot/` (old bot output) as failures or stale entries — those aren't real test results.
- C# tests run from project root via `dotnet run --project PLang.Tests` (different runner, different rules).

### Stale-binary trap

`plang --test` uses `PlangConsole/bin/Debug/net10.0/plang` — a pre-built
executable, **not recompiled per session**. Bot runners inherit this binary
across sessions. Phantom failures with shapes like `Action '<module>.<action>' not found`
or `(null)` reads of `%!<infra>%` properties — for symbols that exist in
source on the current commit — mean a stale binary scanned via reflection,
not a real bug.

Before claiming any PLang test result, rebuild from clean:

```bash
rm -rf PlangConsole/bin PlangConsole/obj PLang/bin PLang/obj \
       PLang.Tests/bin PLang.Tests/obj \
       PLang.Generators/bin PLang.Generators/obj
dotnet build PlangConsole
cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test
```

The C# suite is immune (`dotnet run --project PLang.Tests` recompiles
in-place). Only `plang --test` is exposed to the trap.

Do **not** delete `Tests/**/.build/` — those are tracked `.pr` files, not
build artefacts. The "NEVER delete .build folders" rule above applies.

## Mutation Testing (announce first)

Before editing production source to run a mutation/deletion test — deliberately
breaking behavior to confirm a test catches it — say so in plain text first:

> **Mutation test:** about to temporarily edit `<file>` (`<what changes>`) to
> verify `<which test/finding>`. Will revert immediately; nothing committed.

This is a legitimate and expected technique (testers, reviewers). The
announcement exists only so a watching human never has to wonder whether a
source edit to a security-relevant file is intentional. Rules:

- Announce **once** before a batch of mutations, not per file.
- Always revert before moving on; end with `git status` clean.
- Never commit a mutation — source stays untouched in the final diff.

## Debugging
- `plang --debug` — debug all steps
- `plang '--debug={"goal":"Start"}'` — debug specific goal
- `plang '--debug={"goal":"Start","step":3}'` — debug specific step
- See `cli_reference.md` (auto-loaded into memory) for the full property bag.

## Learning
- When corrected about PLang architecture, **add the insight to `Documentation/Runtime2/good_to_know.md`**
- Read `good_to_know.md` before making architectural assumptions

## Proposing CLAUDE.md / character changes

Do **not** edit CLAUDE.md or character files directly. Two reasons:

- **Agent-level `CLAUDE.md` files are overwritten on next restart** — edits are silently lost. (This applies to per-agent CLAUDE.md, not this repo CLAUDE.md.)
- **`characters/*/character.md` is read-only** on most workspaces (`EROFS`).

The repo `CLAUDE.md` you're reading right now does persist, but it's docs-owned — same proposal workflow.

**To propose any change** to a CLAUDE.md or character file, append to `.bot/<branch>/claude-md-proposals.md`:

    ## <author> — v<N> — <date>
    **Target:** <path>
    **Why:** <one paragraph — what gap, what evidence, why now>
    **Proposed change:** <exact text to add/replace, in a fenced block>

See prior branches' `claude-md-proposals.md` files for examples. Docs picks proposals up during a docs pass and applies the ones that hold up.

**Reviewer bots** (codeanalyzer, security, tester) do NOT propose CLAUDE.md changes on their own — only on explicit user request after a real incident on the branch. When filing under that exception, note it in the proposal footer.

## Todo Capture
When the user writes "todo:" or "dodo:" (typo), append to `Documentation/Runtime2/todos.md` with date and context. Ask at most one clarifying question. Accept dismissals ("n", "no", "nah", "neibb") and move on.
