# Events

An event is a call that runs when a moment comes: `before each goal, call Audit` binds the goal `Audit` to the start of every goal. This page is for the developer working on Runtime2; the page for a program's author is `doc/events.md`.

```plang
Start
- before each goal, call Audit
- before each goal in api/*, call CheckIdentity
- after llm.query, call ShowCost
```

## What an event is

An event (`PLang/app/event/this.cs`) is an item's moment, the side it runs on (`before` or `after`), and what it runs. It has four kinds, each its own class under `PLang/app/event/`:

- `call`: what `on.event` binds, a goal call (`event/call/this.cs`);
- `clause`: an action's own `on.error`, `on.cache`, `on.timeout` (`event/clause/this.cs`);
- `code`: a C# handler, as debug's watch or the test runner bind (`event/code/this.cs`);
- `inherited`: a maker's event a sub app took in (`event/inherited/this.cs`).

A moment is an `on` (`event/on/this.cs`): an item's `before` and `after` are its start, and the named moments (`load`, `create`, `set`, `write`, `error`, …) are each an `on` with their own `before` and `after`. A step binds with the item's path to the moment: `%!app.type.goal.on.start%`, `%!channel.audit.on.write%` (`module/on/event.cs`).

Inside the called goal `%!event%` is the event (`%!app.event.current%`, `event/list/this.cs:41`, through `os/system/shortcut/event.goal`): `%!event.name%`, `%!event.when%`, `%!event!item%` (what it ran for) and `%!event!data%` (the value so far; a failure is `%!error%`).

## Where events live: on the actor

An event lives on the actor that bound it (`actor/this.cs:47–50`, `actor.event`), in the order bound. `%!app.event%` answers the asker's actor's events (`app/this.cs:137–143`), as `%!app.call%` answers the asker's calls. A goal run as another actor never sees them.

## When they load: the first ask

An actor's events are lazy (`event/list/this.cs:5–17`). Nothing is loaded at an app's start. The first ask or add loads them, whoever asks, once; every other asker waits on the same load (`Loaded`, `:155–162`). A load that fails is not kept: what it joined comes off, its askers get the failure and the next ask loads again (`:187–194`). So a system event that fails to load fails closed: no ask goes on with the events half loaded.

The load does two things, in this order (`Load`, `:169–195`):

1. a sub app's actor takes in its maker's events of its role (`:181–183`), each as its own `inherited` event;
2. the actor runs its system events, the files of `system/event/` (`Run`, `:224–234`).

The maker's events join first, so they run first. A sub app's own file replaces what its maker bound in the goal at the same address (`:182`: an inherited event bound in a file the sub app has is not taken in).

## How an item's list fills: pull and push

An item's list of events (a goal's, a step's, a catalog action's, a type's, a channel's) is made on its first ask with every event that applies to it, in the order bound (`On`, `List`, `:53–98`), and is never asked for again. An event bound after that is added to every list already made that it applies to (`Add`, `Join`, `:105–122`); a list not made yet gets it when it is. A set of a variable nothing is bound on makes no list (`Moments`, `:72–81`).

A list is kept by what stays around, in a map owned by the actor's events, so it goes with the actor.

## One question, answered by the thing bound on

"Does this event apply to this item?" is asked at a load and at an add, and one member answers it: `event.Item.Has(item)` (`:96`, `:118`). The item bound on answers for itself: one goal, one catalog action; a type answers for its items (each goal, each action); a module for its actions; a pattern by the goal's path (`goal/pattern/this.cs:29`); a variable by its name.

## By pattern

A name with a wildcard names goals, not one goal (`goal/list/this.cs:214–215`): `%!app.goal["/api/*"]%` is the pattern of them, never the goals that match today, so a goal loaded later under `api/` is named too (`goal/pattern/this.cs:3–8`). A pattern is rooted as a plang path is (`goal/pattern/this.cs:13–19`): `/api/*` from the app's root, `api/*` in the folder of the goal whose step names it. `..` in a name is refused (`GoalNameClimbs`, `goal/list/this.cs:210`). A pattern never runs (`NotAGoal`).

## A variable's moments

A variable's own moments are its named path: `after %count% is set` binds on `%!app.variable["count"].on.set%`. `%count.on…%` is its value's. `%!app.variable["x"]%` is the variable whether or not it is set (`type/item/variable/this.cs:105–117`).

## Plang's own goals: `event.setting.goal`

The goals under `system/` (the builder, the shortcuts) are plang's own. `event.setting.goal` is `app` (the default) or `all` (`event/setting/this.cs`). Under `app`, an event bound on many (a type, a pattern, a module: "each goal", "each action") is skipped inside plang's own goals, system events included; an event bound on one named item runs. Under `all` everything runs. It is read when a side runs (`event/on/side.cs:53–58`), so a setting changed mid-run is heard at the next side. The one member asked is `event.Skips(goal)` (`event/this.cs:52`).

A sub app's `system/` goals are its own code, never plang's for its maker's events, so an upload cannot put its goals under `system/` to escape its maker's "each goal" events (`event/this.cs:47–53`: skipped only for the os's goals, `goal.App is null`, and the running app's own copies).

## System events: `event.setting.system`

`event.setting.system` is `load` (the default) or `skip` (`event/setting/this.cs`). Under `load`, an actor's events, when they first load, run each file of `system/event/` as that actor (`event/list/this.cs:201–218`): the root app's from its own folder where it has the file, else the os's; a sub app's only its own, each replacing the inherited event bound in the file of that name. The first os event is `os/system/event/Hello.goal`, bound on the app's start.

## Sub apps: `Sub: inherit | none`

`on.event` takes `Sub` (`module/on/event.cs`, `event/Sub.cs`), `inherit` by default. `none` keeps the event in the app that bound it: a sub app does not take it in (`event.IsInheritable`, `event/this.cs:42`). A sub app's actor takes in its maker's events when its own first load runs, each as an `inherited` event that runs the maker's, for the sub app's actor of the maker's role (`event/inherited/this.cs`). It is the maker's policy: the sub app cannot take it off (`EventNotYours`, 403), and a failure raised inside a maker's event is not offered to the sub app's own `on error` events (`Takes`, `event/this.cs:58`, `event/on/before.cs:63`), so an upload cannot swallow its maker's checks.

## A call finds its goal in its caller's app

A goal a call names is found in the app its caller goal belongs to, never in the app it runs in (`goal/list/this.cs:191–205`). An os goal's call finds its goal in the running app (its `system/` copy first, then the os's); a maker's goal's call, running in a sub app, finds it in the maker's app.

## A snapshot

A snapshot keeps the `call` events (`call/this.cs:52`, `IsSaved`), per item; an inherited event is not saved. A resume loads the actor's events first, then binds what the snapshot kept, and each inherited event stands for one saved event of the same action on the same moment, which runs once (`event/list/this.cs:238–302`). See `snapshots.md`.
