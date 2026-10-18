# coder — item-doors — summary

## Version
v2 (stage 1, step 2: lists born with the app's type; item-doors 32–37)

## What this is
Every plang value holds the one type object its app made for it (item-doors 33: the maker hands it in, a value
never builds its own). v1 moved the scalar types; v2 does lists, the program nodes, and the type system's own
bootstrap. Decisions are numbered per branch now (item-doors 1–39).

## What was done
- **App start order** (`PLang/app/this.cs`): the type list is made before the actors (34).
- **One object per kinded type** (`type/list/this.cs`, `Full`): the type list keeps each full type it makes, one
  per entry, kind, strict and template (35). The type list itself is born without a type and answers
  `{list, type}` from itself; the root and the type list are the item base's two bootstraps (`item/this.cs`).
- **list and list<T>** (`type/item/list/this.cs`, `this.Generic.cs`): every constructor takes its type
  (required); the `_kind` field, the Type overrides, the `{ Template = }` stamps and the dead `Empty()` are gone.
  Template reads are `Type.Template`. A plain list re-tags into `list<T>` through `list<T>.Create`, called from
  `Clr(target, context)`.
- **12 list subclasses** pass the app's type. goal, step, action (and keep/loop/clause) and test are born taking
  the app's type list and make their own node lists (36); their `= new()` defaults and Type overrides are gone.
- **Makers** pass `context.App.type.list[...]`: readers (json, list, goal, step, action, formal), modules,
  sqlite, signature, test list/report, call frames (the call list takes its audit list).
- **Settings** (37, interim): the one apply walk gives a list member the values leave out an empty list of its own
  class; `actor/setting Make` runs it, so a fresh setting has its defaults. `goal.list.setting` visibility: none
  named lists the public goals.
- **Interim bare types, waiting on item-doors 39** (singular nodes for plurals): `dict.Keys`, input mouse/key
  `Modifiers`. The variable parser (text alone, no app) keeps bare `{list}` types too.
- **Tests:** ~580 test sites updated (four agents, then built and verified here). New: `OneTypeObjectTests`
  (the 34.4 check), `PrByteEqualTests`.

## Checks
- C# sweep: only the known baseline reds (6 Wire snapshot/resume, ImageFromBytes) and the surface pin (re-pinned).
- plang tests: 15 failed / 11 no .pr / 1 rebuild needed — identical to the pre-step-2 commit 19fe320a5.
- .pr bytes: every readable .pr written by today's writer reads and writes back the same bytes. The .pr files
  on disk differ from today's writer (427 of them, `path` vs `file` in the goal header): identical on the
  baseline, a cached rebuild clears it, the build needs the decider key (not set here).

## Next
- item-doors 39: `dict.key`, `modifier`, setting `file`/`variable` as singular nodes with `.list`.
- Step 3: every other value constructor takes its type, required (number, image, choice, null, text, date,
  setting, separator, content, …), and the stand-in goes.
- Step 2 (kinds per app): `type/this.cs` KindOfClass goes.

## Code example
```csharp
// a list is born holding the app's type its maker hands it
var steps = new global::app.goal.step.list.@this(context.App.type.list[typeof(global::app.goal.step.list.@this)]);
// a program node takes the app's type list and makes its own node lists
var goal = new global::app.goal.@this(context.App.type.list) { Name = "Start" };
```
