# PLang Project

## PLang Syntax (v0.1 builder limitations)
- Cannot combine two modules in one step (e.g., `if + set` must be separate steps)
- foreach always calls a goal, does not support sub steps. Syntax: `foreach %products%, call DoProduct item=%product%`, `item=%variableName%` not `%variableName%=%item%`
- Simple set statements work: `set %step.Name% = %stepResult.method%`
- A comment line (`/ …`) describes the NEXT code line. A goal's description goes above its name; a step's comment goes above the step. Never put a goal's description under its name: it would become the first step's comment.
- A folder's docs in a plang app are `start.md` (as `Start.goal` is the entry), not `readme.md`.

## Runtime2 Conventions
- **`app/` is lowercase + singular** for PLang vocabulary (`actor`, `goal`, `variable`, `channel`, `error`, `event`, `filesystem`, `format`, `keepalive`, `snapshot`, `tester`, `type`, `config`, `callstack`, `data`, `module`) and PascalCase for C# infrastructure (`Attributes`, `Diagnostics`, `Services`, `Statics`, `Utils`). Seven engine concepts (`Cache`, `Builder`, `Callback`, `Settings`, `Modules`, `Code`, `Debug`) merged with their action-module counterparts under `app/module/<name>/` — no separate top-level folder remains for those. **plang's nodes are lowercase.** A concept that maps to a folder is a lowercase member (`app.type`, `app.goal`, `app.variable`, `app.module`, `app.data`, `app.actor`, their `list`, `current`, `all`, `on`), so `%!app.type["text"]%` ↔ `app.type["text"]` ↔ `app/type/item/text/this.cs`. A value's facts may stay PascalCase in C# (`Name`, `Count`, `Created`): plang reads members ignoring case, and a .goal writes them lowercase. **Nodes lowercase, verbs PascalCase:** lowercase covers a concept's nodes; methods stay PascalCase (`Start()`, `Value()`, `Add()`, `Module(name)`) — plang never calls a verb through a path, so the uppercase marks the line where C# executes. C# plumbing plang never navigates stays PascalCase (`App.Debug`); a C# keyword keeps its `@` (`app.@event`); an item's own `Type` (its type entity) stays. **One keyword carve-out**: `app/filesystem/Default/` stays PascalCase because `default` is a C# keyword. Two PLang action renames fell out of the rename: `app.run` → `environment.run`, `builder.app` → `builder.load` (both temporary names, deliberate naming pass deferred).
- **`app.X` is the collection node, not a wrapper.** Each concept `X` exposes its collection at `app.X` (type `X.list.@this`, folder `X/list/this.cs`), owned once by the singleton `app` (or `actor` for `channel`). Select with `app.X["name"]`, enumerate with `app.X.list`. A concept that execution flows *through* also has `app.X.current` (e.g. `app.goal.current` reads `CallStack.Current.Action.Step.Goal`); a concept nothing is ever *inside* (`type`, `channel`, `event`, `module`, `format`) has no `.current`. There are no "entities vs services" — only collections, some with a current. The collection never lives on the element and is never a flat `App<Plural>` property (the deleted `AppGoals` / `AppChannels` / `AppEvents` / `AppModules` aliases were that smell). **Registry = selection + lifecycle; all behavior lives on the element** — a type-switch (`is X.subtype`) inside a registry is misplaced behavior; push it onto the element as a virtual member. `module` is a no-`.current` service (action modules are dispatched, not navigated): `module/this.cs` = `module.@this` is the action registry reached at `app.module` (the lookup verb `app.Module(name)` stays PascalCase), no `app.module.current`.
- **`@this` convention**: Every folder's primary class is `@this` in `this.cs`. Consumers use global aliases (e.g., `global using Step = ...Step.@this;`). Within parent namespaces, use `ChildNamespace.@this`.
- **Goal properties**: use `Path` and `PrPath` (relative), not `FilePath`/`PrFilePath`/`RelativePath`
- **Step.Goal**: has `[JsonIgnore]` to avoid circular reference in serialization
- **v0.2 .pr.json format**: single file with all steps
- **Lazy params**: Source generator emits a `partial class` extension on the action record itself (no separate `*__Generated` record) — properties resolve `%var%` lazily on first access via `Action.GetParameter(name).As<T>(Context)`
- **A reference has one definition: `app.type.item.variable.parser`.** Never write a regex (or a hand scan) for `%…%` — ask the parser (`new parser(text).Variable`, `.Read(at)` for one at a position, `.Path()` for a path from a value), or ask the value (`item.Variable` / `HasVariable`). A variable is `Text` + `Code` (its hops: `variable`, `property`, `index`, `method`); it reads through `Start(context)` and writes through `Set(value, context)`. The variable store (`context.Variable`) takes root names only — a path goes through a variable. A stored row writes its `"variable"` list; an authored marked row without it is PrFormatOutdated.
- **Handler naming**: records = action name (`set`, `save`), handlers = `SetHandler`, `SaveHandler` (partial). An action's entry method is `Start()`; the generated dispatcher is `ICodeGenerated.Start()`, an explicit interface member, so it shares the name in one partial class — start an action through `action.Start(context)`, never the handler directly.
- **`ICodeGenerated`**: added automatically by the source generator — handlers never implement it directly
- **Owners take values; handlers open carriers.** A type's operation takes plang values (items: `number`, `text`, `path`), never `data<T>` carriers and never CLR primitives. The action handler opens its carriers through `Use` (`Path.Use(p => Pattern.Use(pat => p.List(pat, …)))`), whose failure is the action's answer; for several optional settings, the generated `Given()`. The CLR lowering happens only at the real boundary inside the owner (the System.IO call, the .NET API).
- **`Data`**: universal result type with `Value`, `Properties`, `Error`, `Success`, `Ok()`, `Fail()`, `Merge()`. Extended via Properties.
- **`Action.Return`**: `List<Data>?` — simple list of return variable mappings, no wrapper class
- **Action `Start()` returns are typed via the signature.** `Task<Data<T>>` for concrete T (catalog: `→ returns T`), `Task<Data<object>>` for genuinely polymorphic returns (catalog: `→ returns data`), bare `Task<Data>` for forwarders that return a `Data` produced elsewhere (`goal.call`, `llm.query`, `output.ask`, condition evaluators); the catalog reads it as `→ returns item` (`goal/step/action/this.Schema.cs:61`). `Modules.Describe()` reads the signature; `action.@this.ReturnTypeName` carries T's PLang name; no separate `Type=` parameter — the `Data<T>` wrapper carries it. **Footgun:** `data.@this<T>`'s implicit operator `@this<T>(T value)` silently double-wraps when `T = object` and the source is itself a `Data` (`Data<object>{ Value = Data<bool>{...} }`). For polymorphic forwarders (`goal.call`, `llm.query`, condition evaluators) that return a `Data` produced elsewhere, stay on bare `Task<Data>`; for owned-construction actions, use `data.@this<object>.Ok(value)` — never `return innerDataInstance;`. Full rule + migration status: `Documentation/v0.2/good_to_know.md` "Action `Start()` returns are typed — and the `Data<T>` implicit-operator footgun".
- **Truthiness — `IBooleanResolvable`.** A value's boolean meaning belongs to the value, not to `Data`. `Data.ToBoolean()` is the sync fallback (null/false/0/"" falsy); do **not** add type-specific cases. A type that knows its own truthiness implements `app.data.IBooleanResolvable` (`Task<bool> AsBooleanAsync()`) — `path` does, where truthiness means "does the resource exist" (stat for `FilePath`, HEAD for `HttpPath`). Because the probe can be I/O, the condition-evaluation pipeline is **async**: `IEvaluator.Evaluate` returns `Task<data.@this>`, `Operator.Evaluate` is `Func<data.@this?, data.@this?, Task<bool>>`, `assert.IsTrue`/`IsFalse` are async. A new operator or evaluator must `await`. Full rule: `Documentation/v0.2/good_to_know.md` "Truthiness — `IBooleanResolvable` and async condition evaluation".
- **Action prose lives in markdown, not attributes.** Class shape (parameters, types, modifier role, defaults) goes in C# attributes on the action handler. **Prose** (Description, Notes, Examples) goes in `os/system/modules/<module>/{module,<action>}.{description,notes,examples}.md`. `[Description]`/`[ModuleDescription]`/`[Example]` no longer exist on action handlers — don't add them back. Per-action Notes/Examples render in the writer's Properties call (one `llm.query` per goal) **only for the actions the decider listed for a step**; `os/system/builder/llm/Properties.llm` keeps only the cross-cutting kernel. Each catalog element exposes its teaching as lazy file items (action: `PLang/app/goal/step/action/this.Schema.cs`; module: `PLang/app/module/this.cs`) under `os/system/modules/<module>/` — an absent file is falsy; there is no load-time orphan scan. Full guide: `os/system/modules/catalog.md`.
- **No `System.IO.*` reaches in production C#.** Filesystem access must go through `app.type.path.@this` verbs (`Read`, `WriteText`, `WriteBytes`, `List`, `Stat`, `ExistsAsync`, `MoveTo`, `CopyTo`, …) — `path.Read` lands a reference (`file`/`url`/`directory`), its content the reference's value and its raw bytes `content.Content(context)` (`type/item/content/this.cs`, the base of `file` and `url`). A value that is plain bytes says so with `item.Plain` (binary's bytes; null for every typed value): a file takes plain bytes as they are, whatever its name, and writes any other value in its format. Every verb passes through `FilePath.AuthGate(verb)`, the only thing stopping out-of-root reads/writes. `System.IO.File`, `System.IO.Directory`, `System.IO.FileInfo`, and `System.IO.Path.Combine`/`GetDirectoryName`/`GetFullPath`/… are banned. A handler reaching there bypasses the actor's permission model. Filesystem paths live as `path.@this` in interior C#; `string` only appears at the perimeter (CLI args, JSON-on-disk shape); the app's anchors are paths, `app.filesystem.root` and `app.filesystem.os` (`%!app.filesystem.root%`, `%!app.filesystem.os%`). The crossing rule: `path.Resolve(rawString, context)` once at the perimeter. Build-time gate: PLNG002 at **error** severity — PLang and PlangConsole build clean with zero PLNG002 warnings as of the `purge-systemio-from-actions` merge, and any regression fails compilation. Exempt: `app.type.path.**` (verb surface uses System.IO post-AuthGate), `Path.DirectorySeparatorChar`/`AltDirectorySeparatorChar`/`PathSeparator`/`VolumeSeparatorChar` (constants, not IO). `.Absolute` discipline: any reach for `.Absolute` outside `app.type.path.**` MUST `await path.Authorize(verb)` first — verbs do this automatically; manual Authorize only for take-over APIs that need the raw string (sqlite, `Assembly.LoadFrom`). Full rule + migration status: `Documentation/v0.2/good_to_know.md` "System.IO Is Banned in Production C#".
- **Data is not enveloped.** Data IS the wire shape — `{name, type, value, properties, signature}`. Do not introduce parallel wrapper types ("Envelope", "Wire", "Wrapper") for Data's serialization shape; the wire shape is Data's own shape with `[Out]` filtering. If you find yourself building a parallel type to bypass `[JsonIgnore]`, the right answer is to add an `[Out]`-aware filter and let `app.data.Wire` (renamed from `WireJsonConverter` on `data-normalize`) handle the wire layout. The historical `Envelope` class on `plang/Data.cs` (deleted on `data-serialize-cleanup`) is the load-bearing example of the smell. The single wire serializer is `application/plang` (the `+data` variant has been merged in); signing fires sign-if-missing inside `Wire.Write` — owners do not call `EnsureSigned()` at egress boundaries. The value slot is built via `data.Normalize(View) → IWriter`, so a new domain type ships by tagging the members that should show: `[Out]` (the wire), `[Store]` (persisted), `[Debug]` (a dump). An `item` structure is then written as that tagged bag by the base `Output` — no override, and **do not** add a `JsonConverter`. A type that writes its own flat form (a path's location, an error's shape) states `Output => Write` once on its base. A type from the assembly that tags nothing for a view is refused on the wire (`NoWireContract`) and written by its name in a dump — a runtime structure (actor, binding) is never walked. Full background: `Documentation/Runtime2/data-spec.md` §15a + §16 + §16a, `Documentation/v0.2/callbacks.md` "Sign-if-missing — the converter does it", `Documentation/v0.2/good_to_know.md` "Domain types ride the wire as property bags".
- **A program's error answers; it travels as an exception only where no result can.** A method that can return a result answers `Fail`/`FromError` with the error's own key. Only where no result can be returned (a constructor, a stream callback, a member with no Data return) does it throw — an `app.error.AppException` (or subclass) carrying its `Error` whole: `throw new AppException(error)`. Every catch answers `ex.Error` (a `catch (AppException ex)` clause, no `is`/`as`/`switch` inside a general catch). `Error.FromException(ex)` is the one door from any exception to an Error: a carried one comes back whole, anything else is `ServiceError` (500) naming its C# type in the message — never a key built from the class name. **Never crack a caught Error** into message/key/status to rebuild it: answer it whole, or make your own Error holding it as its cause (`Error.list`); the frame's `Record` already stamps where it happened. A throw that stays a plain .NET exception means plang itself is broken (a constructor guard, a boot invariant).
- **A value always writes itself.** Every write is `value.Output(writer)`: `item.Output(writer) => writer.Write(this)`, non-virtual, and each writer hands the value itself, typed, so C# picks the value's overload for that writer (`Write(IWriter)`, its own write; `Write(text.Writer)` and `Write(formal.Writer)` only where a value looks different, e.g. a type in text is its word `list<path>`, an action in formal is its call). The code for a look lives in the value's own class. A writer is made with its view (`View.Store`, `View.Out`, `View.Debug`, constants) and its asker (`writer.Context`, never null); no value reads the view: the view shows its tagged members and decides whether a value that holds variables is kept as written (Store), used (Out), or both (Debug: `as written → value (type)` or `not set`). **A kind never writes**: a kind whose difference is how its value is written (duration's `short`/`iso`, size's `iec`/`si`) is a subclass of its type that writes and parses itself; any other kind (a number's precision, a file format) stays a kind and writes nothing. A C# object that isn't a plang value is written by the value's own walk over its `Host`'s tagged members. A value's content is opened at the last moment by the layer that sends it out (the channel), never inside the value's own write.
- **No `Console.*` writes in production C#.** Channels exist to make I/O redirectable; `Console.WriteLine`/`Console.Error.WriteLine` bypass that. Diagnostics → `await context.App.Debug.Write(...)` (debug channel, gated on `--debug`). User-facing chatter → `await context.Actor.Channel[global::app.channel.list.@this.Output].WriteText(...)` — the list selects (`this[name]` for the defaults, which `Verify` guarantees; `Get(name)` null on a miss for a user-named channel), the channel writes (`WriteText`/`WriteAsync`); do **not** route through `Debug.Write` (its `IsEnabled` gate would silence it without `--debug`). Interactive prompts use a two-call pattern across the split `output`/`input` pair (write via `output`, read via `StreamReader(input.Stream, leaveOpen: true)`). Permitted exceptions: `Console.IsInputRedirected`/`IsOutputRedirected` (queries, not writes) and `PlangConsole/Program.cs:26` (process-boundary last resort if channels failed to wire). Full rule + test-fixture pattern: `Documentation/v0.2/good_to_know.md` "Console.* Is Banned in Production C#".
- **A flag is false by default.** A bool option (action or setting) is `[Default(false)]` and named for what the words say to turn it on (`Parallel`, `Descending`) — never the opposite with `[Default(true)]`, never a negated name (`DontWait`). The builder writes a property only when it thinks the step names it, so a default-false flag left out means what the step said; a default-true one invites a false the step never said. When the common case is "on" (keep empty pieces, use the cache), the option is a choice whose values name both behaviours, the common one the default (`Empty: keep | drop`, `Empty=drop`) — never a default-true bool, never a two-word flag (`DropEmpty`). Full rule: `Documentation/v0.2/conventions.md` "A Flag Is False By Default".

## OBP Shape Smells (audit before writing or reviewing C#)

When reading or writing C#, check the diff against the named smells. A hit means the shape is wrong and the fix is structural, not a line edit. Names are canonical — cite findings by name; worked examples and grep tells live in `Documentation/v0.2/obp-smells.md`.

**Shape:**
- **glued name** — a compound camelCase name (`ListName`, `buildExecutionPath`). Write it as a dot path, one word per segment (`setting.build.execution.path`), and check it navigates: each segment an owner whose member is the next. A path that doesn't navigate (`list.name`: a list has no name) is a flat copy or a misplaced member; the dot path shows the real owner (`list.variable.name`, so take `list`).
- **creative name** — a variable, field or parameter named for its role or its origin instead of what it holds: `declared` for a type, `held` for an action, `wrapped`, `fresh`, `incoming`. Name it what it is: a type is `type`, an action is `action`, a value is `value`. Two of a kind in one scope are told apart by what they are (`type` and its `kind`), never by an invented adjective.
- **naked collection** — bare `List<T>`/`Dictionary`/`HashSet` as public state; its add/lock/evict discipline lives in other files. Fix: its own `X.list` type (private backing, own `Add`, `IReadOnlyList<T>` surface), exposed as a **singular** property naming the concept (`callStack.Error`).
- **middleman** — a parent proxying what it owns (`AddError(...)` wrapping `Error.Add(...)`). Expose the node; domain operations belong on the collection type, never on the parent.
- **cross-file lock** — `lock (other.X)` taken from outside `other`'s class; the type that owns the data isn't the type that owns the discipline.
- **stored twice** — the same logical thing held in two types with overlapping meaning (similar names, same element type, same role). One `X.list` under the concept; never a domain word for the container (`trail`, `ErrorLog`).
- **split lifecycle** — allocate-here / mutate-there / clean-up-elsewhere; one collection's life across three files.
- **flat copy** — holds a reference AND scalar mirrors of properties reachable through it (`Foo Foo` + `Path`/`Name` copies of `Foo.X`); the mirrors drift and every construction site double-pays. Route through the reference; keep at most one summary field for the null-reference failure path.
- **raw hand-off** — producer returns raw, every consumer applies the same transform (`.TrimStart('/')`, `.ToLowerInvariant()` at 3+ sites). The discipline belongs on the owner (`Goal.RelativePath`). Grep: `\.{PropertyName}\.(TrimStart|TrimEnd|ToLower|ToUpper|Replace|GetDirectoryName|Substring|Split)`.
- **stray helper** — `Helper.X(thing)` that should be `thing.X()`; a method that missed its type. Every private static helper in a class is a suspect.

**Value layer:**
- **broken seal** — a courier (a handler forwarding Data, variable memory, callstack, channel routing, signing, the wire envelope) reads `data.Value as/is X` mid-flight. Only leaves open the package: handlers declaring a typed `Data<T>` slot, and the value's own per-format serializer. Grep: `\.Value (is|as|switch)` outside files declaring `Data<T>` parameters.
- **opened box** — a leaf cracks carriers into primitives for a helper (`number.Round(await Value.Value(), await Decimals.Value())`). The value owns its operations; operands ride whole: `await Value.Round(Decimals)`, `await A.Resize(Width, Height)`. If you opened the box to pass what was inside, pass the box.
- **clr leak** — `.Clr` lowering anywhere but a real .NET/3rd-party/storage boundary; high `.Clr` density means a CLR-centric design.
- **late stamp** — construct-then-stamp (`new X(...) { Context = ... }`, `Context ??=`); context is a private non-nullable field set at construction.
- **envelope** — a hand-built wrapper around Data's shape to carry what `[JsonIgnore]` keeps off the wire or out of the store (`new Dictionary<string, object?> { ["Value"] = …, ["Model"] = … }`, unpacked by `entry.Name == "Value"`); the field list is rewritten at every pack/unpack site and the value is often kept twice. Store the Data whole and tag what persists (`[Out]`/`[Store]`); a module wrapping Data to cache it is doing the cache's job. Tells: `["Value"]` or `Name == "Value"` outside `app/data/`.

**Design alarms:**
- **fork** — two execution paths for one operation: behavioral `if`/`switch`, generic fallback beside per-type handlers, type-switch in a registry (push onto the element as a virtual member), optional-override branch.
- **verb+noun** — any compound name where one half is a verb (`BuildTypeEntries`, `CoerceToKind`, `GetParameters`); only boolean `IsX`/`HasX` are exempt. Never allowed — always diagnostic of an object doing another object's job or proxying what should own itself.

If removing one line of choreography requires editing three files, those three files are one missing type.

Full catalog with worked examples: `Documentation/v0.2/obp-smells.md`. The pattern (3 laws + rules): `Documentation/v0.2/object_pattern_formal.md`. Audit procedure: `Documentation/v0.2/obp-scan.md`.

## The locked core

Ingi has **locked the core's surface.** These are the load-bearing shapes the rest of the system is built on, and their surface does not change without his say-so:

- **`app`'s members;**
- **the type system** — `type`, `type.list`, `kind`, `property`, and `item`'s base virtuals;
- **the serializer** — the readers and writers, `IWriter` / `IReader`, `format`, and `Wire`;
- **`data`;** **`channel` and `stream`;** **events** — the event door (`event.on`), the core events and `[Events(...)]`; **call and variable memory;** the **module registry and catalog;**
- **the core value types' public surface** — `text`, `number`, `bool`, `list`, `dict`, `path`, `datetime`, `duration`.

**Inside them, behaviour fixes and private refactors only.** An **addition** to any locked surface — a member others call, a virtual, an attribute, an interface member, an `app` property, a new core type, a plang-visible member — is **a short proposal to the architect, who brings it to Ingi; nothing is built before his yes.** Do not add to a locked surface on your own judgement, however reasonable the addition seems.

**New needs go to the edge, not the core:** a module owns its own types, and a module declares its own events. Reach for a module-owned type or a module-declared event before you reach for the core.

**The teeth:** a surface golden runs in the gate; a change to a locked surface fails the build until it is re-pinned **with the decision that approved it.** An unapproved surface change cannot merge.

## Source Generator
- PLang.Generators: netstandard2.0, IIncrementalGenerator
- OBP shape: entry `PLang.Generators/this.cs` → `Discovery/this.cs` (Roslyn boundary) + `Emission/Action/this.cs` (per-handler) + `Emission/Property/{Data,Code}/this.cs` (polymorphic per-property)
- Filter out `EqualityContract` (protected, not public) when scanning virtual props
- Generated records must be `public sealed record` to match base access level
- In tests: use `System.Type?` (not `Type?`) to avoid ambiguity with `PLang.Runtime2.Memory.Type`
- **Property kinds (PLNG001 build-time gate)**: action handler properties must be `Data<T>` (or plain `Data`) or `[Code] T`. Anything else fails the build with `PLNG001`. For parameters that *name* a variable (write targets, read-by-name lookups: `variable.set`, `list.*`, `loop.foreach` ItemName/KeyName), use `Data<app.variable.Variable>`. `Variable` implements `IRawNameResolvable`, which tells `Data.As<T>` to skip its `%var%` substitution branch and dispatch to `Variable.Resolve(raw, ctx)` directly — both `value="%x%"` and bare `value="x"` collapse to `Variable { Name = "x" }`. Use sites read `Foo.Value` (Variable's implicit `string` operator covers method-call boundaries; `ToString() => Name` makes interpolation read naturally). Non-nullable `Data<Variable>` slots get a generator-emitted pre-Run guard that surfaces `MissingRequiredParameter` (auto-detected via the `IRawNameResolvable` marker through Discovery → ActionClassInfo → Action emitter, mirroring `[IsNotNull]`).
- **Incremental cache**: `ActionClassInfo` is a record with `EquatableArray<T>` collections (no `IPropertySymbol` references) so Roslyn cache hits on semantically identical inputs. Tracking-name constants on `PLang.Generators.@this` exist for `IncrementalCacheTests`.
- **Test alias clash**: `PLang.Tests/GlobalUsings.cs` aliases `Data` and `Variables` to types. Do NOT create `PLang.Tests.App.Data` or `PLang.Tests.App.Variables` namespaces — they shadow the alias for all sibling test files (CS0118). Convention: use `*Tests` suffix on folder/namespace when mirroring `PLang/app/data/` etc. → `PLang.Tests/App/DataTests/`, `PLang.Tests/App/VariablesTests/`. (Test folder names under `PLang.Tests/App/` stay PascalCase — only the source paths under `PLang/app/` are lowercase.)

## Key Files
- PlangConsole is the executable project (not PLang which is a library)
- os/system/builder/*.goal — the PLang builder written in PLang
- PLang/app/this.cs — the app, root of every concept (`app.@this`)
- PLang/app/goal/this.cs, PLang/app/goal/step/this.cs — goal and step
- PLang/app/module/<module>/<action>.cs — action handlers; PLang/app/module/IClass.cs, ICodeGenerated.cs — handler interfaces
- PLang/app/data/this.cs — Data, the universal result and value carrier
- PLang/app/type/this.cs, PLang/app/type/list/this.cs — the type system and the app's type list
- PLang/app/GlobalUsings.cs — global type aliases for @this classes
- PLang.Generators/this.cs — source generator entry point (`Discovery/`, `Emission/Action/`, `Emission/Property/{Data,Code}/` underneath)
- For full OBP details: `Documentation/v0.2/object_pattern_formal.md` (3 laws + rules), `Documentation/v0.2/obp-smells.md` (named smell catalog)

## Build
- Always run `plang build` without specifying a goal name — it builds everything
- NEVER delete .build folders
- NEVER hand-edit a `.pr` file, by any tool, even to fix only a path. A `.pr` is what the builder writes. When a rebuild can't reproduce one, report it and leave the file as built.
- Use `PlangConsole/bin/Debug/net10.0/plang.exe` for net10.0 builds
- Don't use Select-String in bash — it doesn't work
- **How a build answer is judged** (what Cover, Agree, the decider's stage 2 and the step-fixer refuse): `Documentation/v0.2/build-checks.md`.
- **Teaching-edit rule:** a commit touching `os/system/modules/**/*.md` **or `os/system/type/**/*.md`** re-runs the decider fixtures (`PickListTests`, `ConfirmTemplateTests`, `LineTwinTests`, `PickOptionTests`) and the page goldens (`ModulePageTests` / `TypePageTests`), re-pinning each it moves (a type's description is in the decider's prompt C, so it moves the decider pin too). Run the Explicit `AcceptTheFixture` against the test binary to re-pin, and read the diff before committing a moved pin — a re-pin made under load can capture a worse answer. (`build-checks.md`.)

## Running plang Tests

- All plang tests live under `test/` (lowercase). Never under `Tests/`, `tests/`, `.bot/`, `.build/`, `os/`, or any other tree. Folders inside are singular, and a test lives at the path of what it tests (`test/module/on/cache/`, `test/app/type/`) — never in a folder named for a branch or stage.
- A plan's validation tests live in their own folder, `test/plan/<id>/` (id = the plan's branch), each at the path of what it tests; the plan's index is `test/plan/<id>/start.goal` (each behaviour a comment, the step under it runs its test). Tests that aren't a plan's stay in the concept tree (`test/module/on/…`).
- When a plan is done, its tests move into the concept tree at the same path (`test/plan/<id>/module/on/cache/X` → `test/module/on/cache/X`); `start.md`, `start.goal` and `done.list` stay as the plan's record, so `test/plan/` holds only plans in progress. A moved test's `.pr` is rebuilt by the builder, never hand-edited.
- When running `plang --test`, change directory into `test/` first so discovery is bounded to the canonical location:

  ```bash
  cd test && ../PlangConsole/bin/Debug/net10.0/plang --test
  ```

  Running `plang --test` from the project root will surface stale `.test.goal` files under `.bot/` (old bot output) as failures or stale entries — those aren't real test results.
- **C# tests: use `./dev.sh test <ClassName>`** (e.g. `./dev.sh test ModulePageTests`). It builds only the needed test project (and its `PLang`/`Shared` refs — it **skips PlangConsole**), then runs that test binary directly with a TUnit filter. Do **not** use `dotnet run --project PLang.Tests`: the aggregate project isn't runnable, and a per-project `dotnet run` is 90s+ every call (restore + build + run). `./dev.sh test` with no filter runs the full C# sweep; `./dev.sh build` is the incremental build alone.

### Stale-binary trap

`plang --test` uses `PlangConsole/bin/Debug/net10.0/plang` — a pre-built
executable, **not recompiled per session**. Bot runners inherit this binary
across sessions. Phantom failures with shapes like `Action '<module>.<action>' not found`
or `(null)` reads of `%!<infra>%` properties — for symbols that exist in
source on the current commit — mean a stale binary scanned via reflection,
not a real bug.

Before claiming any PLang test result, rebuild PlangConsole first:

```bash
./dev.sh build   # incremental build of the test projects + PlangConsole; skipped when nothing changed
cd test && ../PlangConsole/bin/Debug/net10.0/plang --test
```

The C# suite is immune (`./dev.sh test` rebuilds the changed project before running).
Only `plang --test` is exposed to the trap.

Do **not** delete `test/**/.build/` — those are tracked `.pr` files, not
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
- `plang --debug.setting.goal=Start` — debug specific goal (a debug setting alone switches debug on)
- `plang '--debug.setting={"goal":"Start","step":3}'` — debug specific step (several options at once: a dict at the setting's path)
- Any setting from the command line: `--<path>=<value>`, `app.` optional — `--channel.setting.format=plang`, `--build.setting.cache=skip`, `--test.setting.parallel=1`. A path no setting takes is refused.
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
