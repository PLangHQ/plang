# The shape of the code

For someone changing the runtime. The question this page answers: where does a thing live, and what is it called?

## `app` is the root

The runtime's root is the class `app.@this`, in `PLang/app/this.cs:19`. A program, a test and a sub app are each an `app`. It owns the concepts a program can reach, as lowercase members: `goal` (`this.cs:167`), `actor` (`:262`), `type` (`:250`), `module` (`:153`), `variable` (`:219`), `error` (`:212`), `test` (`:205`), `shortcut` (`:227`), `filesystem` (`:176`), `store` (`:190`), `parent` (`:255`).

A program reaches them as `%!app.goal%`, `%!app.type["text"]%`, `%!app.actor["user"]%`.

## A concept is a folder

A concept is a folder under `PLang/app/`, and its primary class is `@this` in `this.cs`:

- `PLang/app/goal/this.cs:23` is the goal, `goal/step/this.cs:11` the step, `goal/list/this.cs:12` the goal list.
- A value type is under `type/item/<name>/this.cs`, marked with its plang name: `type/item/text/this.cs:29` carries `[PlangType("text")]`.
- An action of a module is `module/<module>/<action>.cs`: `module/math/add.cs`, `module/variable/set.cs`.

A folder's class is `@this` so that its name comes from the folder. Code outside the folder uses a global alias: `PLang/app/GlobalUsings.cs:3` aliases `Error` to `app.error.@this`, and `:6` aliases `Step` to `app.goal.step.@this`. Inside the parent namespace, a child is written `child.@this`.

## `app.X` is a node

A concept the app holds is reached through a **node**, a generic class in `PLang/app/type/node/`:

- `type.node.@this<T, L>` (`type/node/this.cs:20`) holds the concept's list (`.list`), finds one by key (`Get`), and writes itself as its list. `app.variable` and `app.shortcut` are this kind (`app/this.cs:219`, `:227`).
- `type.node.current.@this<T, L>` (`type/node/current/this.cs:9`) adds `current`, the one in play for the asker: `app.goal`, `app.actor`, `app.test`, `app.error` (`app/this.cs:167`, `:262`, `:205`, `:212`). `%!app.goal.name%` is the running goal's name.
- `type.node.root.@this` is the type system's own node (`app/this.cs:250`).

So a collection lives once, on the app (or on the actor, for what is the actor's), and never on the element. `app.X.list` enumerates; `app.X["name"]` selects.

## Names

- **Nodes and concepts are lowercase**: `goal`, `type`, `actor`, `module`. A keyword keeps its `@`: `@event` (`app/this.cs:126`), `@this`.
- **Verbs are PascalCase**: `Start(context)` (`goal/this.cs:400`), `Name(context)` (`app/this.cs:76`), `Get`, `Find`. A program never calls a verb through a path, so the capital marks where C# runs.
- **C# plumbing a program never navigates is PascalCase**: `Code` (`app/this.cs:159`), `Cache` (`:181`), `Debug` (`:197`), `Build` (`:234`).
- **A value's facts may stay PascalCase** in C# (`Created`, `Version`, `Id`, `app/this.cs:68-99`); a program reads them without regard to case.
- Infrastructure folders under `PLang/app/` that are PascalCase: `Attributes`, `Utils`. The rest are lowercase.

## A module's actions

A module is a folder `module/<name>/`, an action a class in it marked `[Action("name")]` (`module/math/add.cs:5`). `module/this.cs:13` is the module registry. Its teaching prose is not in the C#: it is in `os/system/modules/<name>/`. See [add an action](add-an-action.md).

## Other projects

- `PLang/`: the runtime library. `PlangConsole/`: the command-line program (`PlangConsole/Program.cs`). `PLang.Generators/`: the source generator that writes each action's dispatcher. `PLang.Tests/`: the C# tests. `os/`: goals and teaching that ship with plang (the builder is written in plang, `os/system/builder/`). `test/`: the plang tests.
