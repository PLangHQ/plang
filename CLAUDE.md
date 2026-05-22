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
- **No `Console.*` writes in production C#.** Channels exist to make I/O redirectable; `Console.WriteLine`/`Console.Error.WriteLine` bypass that. Diagnostics → `await context.App.Debug.Write(...)` (debug channel, gated on `--debug`). User-facing chatter → `await app.CurrentActor.Channels.WriteTextAsync(global::app.channels.@this.Output, ...)` (do **not** route through `Debug.Write` — its `IsEnabled` gate would silence it without `--debug`). Interactive prompts use a two-call pattern across the split `output`/`input` pair (write via `output`, read via `StreamReader(input.Stream, leaveOpen: true)`). Permitted exceptions: `Console.IsInputRedirected`/`IsOutputRedirected` (queries, not writes) and `PlangConsole/Program.cs:26` (process-boundary last resort if channels failed to wire). Full rule + test-fixture pattern: `Documentation/v0.2/good_to_know.md` "Console.* Is Banned in Production C#".

## OBP Shape Smells (audit before writing or reviewing C#)

When reading or writing C#, run this checklist. Each item is a yes/no question; any "yes" means the shape is wrong and the fix is structural, not a line edit.

1. **Public mutable collection with rules enforced from outside.** A type exposes `public List<T>` / `Dictionary<K,V>` / `HashSet<T>` and the `Add` / `Remove` / locking / eviction lives in another file. The collection should become its own `@this` type with private lock and `Add(...)` / `IReadOnlyList<T>` surface.
2. **Cross-file lock target.** `lock (other.X)` taken from outside `other`'s class — the type that owns the data isn't the type that owns the discipline.
3. **Same logical thing stored twice across types** (overlapping semantics, similar names, same element type, same role).
4. **Allocate-here / mutate-there / clean-up-elsewhere.** One collection's lifecycle split across three files.

If removing one line of choreography requires editing three files, those three files are one missing type.

Full checklist and worked example: `Documentation/v0.2/good_to_know.md` "OBP Smell Checklist".

## Source Generator
- PLang.Generators: netstandard2.0, IIncrementalGenerator
- OBP shape: entry `PLang.Generators/this.cs` → `Discovery/this.cs` (Roslyn boundary) + `Emission/Action/this.cs` (per-handler) + `Emission/Property/{Data,Provider}/this.cs` (polymorphic per-property)
- Filter out `EqualityContract` (protected, not public) when scanning virtual props
- Generated records must be `public sealed record` to match base access level
- In tests: use `System.Type?` (not `Type?`) to avoid ambiguity with `PLang.Runtime2.Memory.Type`
- **Property kinds (PLNG001 build-time gate)**: action handler properties must be `Data<T>` (or plain `Data`) or `[Provider] T`. Anything else fails the build with `PLNG001`. For parameters that *name* a variable (write targets, read-by-name lookups: `variable.set`, `list.*`, `loop.foreach` ItemName/KeyName), use `Data<app.variables.Variable>`. `Variable` implements `IRawNameResolvable`, which tells `Data.As<T>` to skip its `%var%` substitution branch and dispatch to `Variable.Resolve(raw, ctx)` directly — both `value="%x%"` and bare `value="x"` collapse to `Variable { Name = "x" }`. Use sites read `Foo.Value` (Variable's implicit `string` operator covers method-call boundaries; `ToString() => Name` makes interpolation read naturally). Non-nullable `Data<Variable>` slots get a generator-emitted pre-Run guard that surfaces `MissingRequiredParameter` (auto-detected via the `IRawNameResolvable` marker through Discovery → ActionClassInfo → Action emitter, mirroring `[IsNotNull]`).
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
- PLang.Generators/this.cs — source generator entry point (`Discovery/`, `Emission/Action/`, `Emission/Property/{Data,Provider}/` underneath)
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

