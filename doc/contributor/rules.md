# The rules

For someone changing the runtime. The question this page answers: what rules must I keep, and what stops me if I do not?

Each rule says what it is, how it is held (a build gate, a test, or only review), and where its reason is. The reasons are in [decisions](../decisions/start.md).

## 1. No `System.IO` in production C#

Filesystem access goes through the verbs of `app.type.item.path.@this` (`Read`, `WriteText`, `List`, `Stat`, `MoveTo`, …). Each one passes the permission gate, `AuthGate(verb)` (`PLang/app/type/item/path/this.Operations.cs:48`). `System.IO.File`, `Directory`, `FileInfo` and the rest of the disk-touching names are banned under `PLang/app/**`. A path in interior C# is a `path`, and a `string` appears only at the perimeter (command-line arguments, the shape on disk).

- **Held by:** the build diagnostic `PLNG002`, severity Error (`PLang.Generators/Diagnostics/Plng002.cs:39-50`), and `Plng002SystemIoBanTests` (11 tests, pass). The name-math members of `System.IO.Path` are allowed from one forwarder and the separator constants are exempt (`Plng002.cs:23-37`).
- **When it runs:** in every build, including `./dev.sh build` (which passes `-p:RunAnalyzers=false`, `dev.sh:31`). Run to check this: a throwaway action calling `System.IO.File.ReadAllText` failed `./dev.sh build` with `error PLNG002: 'File.ReadAllText' under System.IO bypasses FilePath.AuthGate`. `PLNG001` (an action property that is not `Data<T>` or `[Code]`) failed the same build with `error PLNG001`. `./dev.sh full` is the pre-commit gate in its own `Gate` configuration with the remaining analyzers on (`dev.sh:11`, `:312-314`).
- **Taking over a file:** a reach for `.Absolute` outside `app.type.item.path.**` must `await path.Authorize(verb)` first (`Documentation/v0.2/bans.md`, "System.IO Is Banned").

## 2. No `Console.*` writes

Output goes through channels, which a program can redirect. Diagnostics go to `context.App.Debug.Write(...)`, gated on `--debug`. User-facing text goes to the actor's `output` channel. The exceptions are queries (`Console.IsInputRedirected`, `Console.IsOutputRedirected`) and the process boundary in `PlangConsole/Program.cs:24`.

- **Held by:** review only. No analyzer and no test was found. A search of `PLang/app/` for `Console.Write` and `Console.Error` finds one string that mentions it in a parser (`PLang/app/type/code/this.Parse.cs:22`) and no call.

## 3. An error answers; it travels as an exception only where it must

A method that can return a result answers `Fail` / `FromError` with the error's own key. Only where no result can be returned (a constructor, a stream callback, a member with no `Data` return) does it throw `AppException`, carrying its `Error` whole (`PLang/app/error/Exceptions.cs:8`). A catch answers `ex.Error` (`catch (AppException ex)`), and `Error.FromException(ex)` (`PLang/app/error/this.cs:274`) is the one door from any exception to an error: an `AppException` comes back whole, anything else is a `ServiceError` (500) naming the C# type. Never take an error apart to rebuild it; answer it whole or hold it as the cause of your own.

- **Held by:** review. The step and the action both follow it (`goal/step/this.cs:153-157`, `goal/step/action/this.cs:333-337`).
- **A plain .NET exception means plang is broken.** The action's catch lets `NullReferenceException` through (`action/this.cs:334`); the step answers it as a `ServiceError`. Finding E is that: a step built to a call that opened an empty value.

## 4. A flag is false by default

A bool option is `[Default(false)]` (or has no default) and is named for what the words say to turn it on (`Parallel`, `Descending`), never the opposite with `[Default(true)]`, never a negated name. When the common case is "on", the option is a choice whose values name both behaviours (`Empty: keep | drop`).

- **Held by:** review. A search of `PLang/app/module/` finds no `[Default(true)]`.

## 5. A run never writes an object its app shares

A sub app's types, actions and goals are layers over what it is made from, so a shared entry is every app's. What a run adds goes to its own layer or its own context. A binding made while a program runs is held on the actor that made it; a type takes no history.

- **Held by:** `SharedEntryWritesTests` (1 test, pass) and `HeldBindingTests` (named, not run), both named in `Documentation/v0.2/apps.md`, "A run never writes an object its app shares".

## 6. Action prose is in markdown, not in attributes

An action's class shape (parameters, types, defaults) is in C# attributes. Its description, notes and examples are in `os/system/modules/<module>/<action>.{description,notes,examples}.md`. The attributes `[Description]`, `[ModuleDescription]` and `[Example]` do not exist (`PLang/app/Attributes/` has no such files). An absent file is not an error; the action just has no prose for that slot (`os/system/modules/catalog.md:80`).

- **Held by:** the page goldens and decider fixtures; see [add an action](add-an-action.md).

## 7. A reference has one definition

A `%variable%` is read by `app.type.item.variable.parser` (`PLang/app/type/item/variable/parser/`). Do not write a regular expression or a hand scan for `%…%`.

- **Held by:** review.

## 8. The handler's result is typed by its signature

`Task<Data<T>>` for a concrete result, bare `Task<Data>` for a forwarder of a `Data` produced elsewhere. The `Data<T>` implicit operator double-wraps when `T = object` and the source is itself a `Data`, so a forwarder stays on bare `Task<Data>`. Details: `Documentation/v0.2/data-internals.md:290-300`.

- **Held by:** review.

## 9. Do not hand-edit a `.pr`

A `.pr` is what the builder wrote. When a rebuild cannot reproduce it, report it and leave it.

- **Held by:** the staleness hash: a test whose `.goal` changed after the last build is `Stale` and is not run.

## Shape smells

When you read or write C#, check the change against the named smells (glued name, naked collection, middleman, flat copy, stray helper, broken seal, opened box, envelope, fork, verb+noun, …). They are named, with worked examples, in `Documentation/v0.2/obp-smells.md`. A file a branch touches is left better than it was found.
