# coder v1 — review of the plan (stages 1, 3, 4) and stage 0

## Stage 0 — done

- **Compile re-recorded** (TypeSafe answers again). Nano answered only step 0; steps 2–3 had no
  entry. The twins disagreed on what that is: C#'s `step.list.Read` refuses the missing step alone,
  python's `check` counted it as a whole-answer problem (every step caught). The python mirror now
  follows C# (`prompt_c.check`). BootstrapTests passes (both refuse [0, 2, 3]). `9f9f98105`.
- **Baseline** of the six suites: `baseline-tests.md` (Modules 38 / Types 23 / Wire 18 / Data 45 /
  Generator 18 / Runtime 24 failing; plang --test 7 pass, 317 stale).
- **v0.1 files deleted**: 552 tracked `NN. stepname.pr` under `os/` (112 `00. Goal.pr`); the 32 v0.2
  files and the folders stay. Six suites: no new failures (two known flakes swapped). `cdacf28a2`.
- **Open:** 98 tracked `NN. stepname.dll` / `.pdb` (the same v0.1 steps' compiled code) under
  `os/apps/{Installer,Plang,Tests,Wallet}/.build` — not named by the ruling; left.

## Review — what doesn't hold (checked in the code)

### Stage 1 (Run → Start)
1. **The deferred handlers can't keep `Run()`.** The generator emits one `await Run()` for every
   handler (`PLang.Generators/Emission/Action/this.cs:393`, in `Execute`). `cache.wrap`,
   `timeout.after`, `event.on`, `mock.intercept`, `on/error` are among the 125 handlers, so they are
   renamed with the rest (or the generator is special-cased — not wanted). The event bindings'
   `Run` (`event/lifecycle/binding/this.cs:44`) can stay, but it's called from inside the renamed
   `Start` bodies (`goal/this.cs:336,374`), mixed verbs until stage 8.
2. **timer's handler class is named `Start`** (`module/action/timer/start.cs:9`, `[Action("start")]`):
   a `Start()` method in it is CS0542. Its C# class needs another name; plang sees no change.
3. **A third plang action named `run`: `callback.run`** (`module/action/callback/run.cs:12`, teaching
   `os/system/modules/callback/run.description.md`). Builder-visible like the other two.
4. **`App.Run<TAction, TResult>`** (`app/this.cs:454`) beside `Run<TAction>` (`:431`); production
   callers in build/llm code. `app.Start()` already exists (`:480`).
5. Other `Run*` entries not listed: `Executor.Run` (`PLang/Executor.cs:15`, from PlangConsole),
   `goal/setup/this.cs:78 RunAsync` (9 test calls), `build/this.cs:76 RunAsync`.
6. Test counts: `.Run(` in 132 test files holds; "179 with RunAsync/RunGoalAsync" is 152. 10 test files
   find the handler's method by name (`GetMethod("Run")`), 14 test doubles declare `Run()`.
7. `environment.run` has no examples file (only `run.description.md`); test has both.

### Stages 3–4 (the registry, the collected type)
1. **`type/this.cs:496-497` has no "own members first".** A type's navigation hands everything to a
   `clr` reflection over the full type: `new clr.@this(parent.Context.App.Type[this], …).Get(…)`. The
   sketch's `base.Get(parent, key)` then `Get(key)` depends on how that reflection answers a miss.
2. **More public list mutators than `Add`/`Remove`:** `Insert` (`type/item/list/this.cs:335,371`),
   `RemoveAt` (`:372`), `Set`→`Put` (`:539`) — an index kept inside the list later needs them all.
3. **More spelling/alias sources for stage 3:** `Get(string, depth)` (`type/list/this.cs:323-343`, turns
   `list<…>`/`dict<…>` into CLR generics), `Contains` splitting on `/` (`:99`), and `SeedAliases`
   (`Registry.cs:142-152`) writing aliases into `_nameToType`.
4. **The registry's stored `Context` also serves `App.Format`:** `Mime` (`:204`) and the identity
   indexer (`:228`, `CanonicaliseKind`). Stage 4's "its stored context goes" needs Format from elsewhere.
5. **Loader's "reserved names" are property names a loaded type may not declare** (`Loader.cs:68-85,
   109-113`: `ReservedCore`/`ReservedShadow`), not reserved type names.
6. **A `type<T>` instance's C# class maps to "clr":** `PlangName` (`:356-376`) sends an unknown class to
   `("clr", null)`, so `app.type.list[typeof(type<goal>)]` would answer `clr`.
7. The value-birth lookups: also `Creatable` (`type/this.cs:358`, `App.Type.Clr(Name)`), `Read` (`:302`,
   `App.Type.Reader`), navigation (`:497`); `:259` is an `Is` check.
8. Line fixes: `Reader` is `:75` (`:203` Mime, `:211` Extension, `:261` `this[System.Type]`); the
   `[LlmBuilder]` filter is `:530/:548`; `list.Generic.cs:26-29` is `Items()` (the storage is the base
   list's); ICreate's static members have default bodies (`ICreate.cs:40,49,58`), so test, timing,
   LlmMessage and type gain it by adding the interface.

Holds as written: the five entry `Run`s, 125 handlers, the call chain, `list.range.Start`,
`GetMethod("Run")` in `this.Schema.cs:53`, test's stopwatch `Start`, `RunGoalAsync`, the ICreate list
(nothing missed), the four concept lists not inheriting the plang list, goal's three dictionaries,
`app/this.cs` members and constructor order (Actor must precede the type: the registry takes
`System.Context`), 80 `App.Type` references in 41 files, ~48 indexer callers.
