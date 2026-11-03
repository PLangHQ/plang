# Events

For someone writing a program. The question this page answers: how do I run a goal whenever something happens?

## Bind a goal to an event

```plang
Start
- set %marks% = 0
- before action output.write call Mark
- write out "one"
- write out "two"
- write out "marks: %marks%"

Mark
- set %marks% = %marks% + 1
```

`before action output.write call Mark` runs the goal `Mark` before each `write out`, so three times here. `Mark` uses `set`, not `write out`, so it does not trigger itself.

## What you can bind to

The forms the `on` module teaches (`os/system/modules/on/event.description.md`):

- `before each goal call X`, `after each goal call X`
- `after step call X`
- `before action file.read call X`
- a channel writing, reading or asking: `before write on "audit" channel call X`

Of these only `before action output.write` and `after each goal` were run for this page. `test/module/on/event` has 3 tests that pass (subfolders for before/after step, before/after action, priority, override, remove and wildcard exist); which forms they cover was not checked.

## Each goal, each step, each action

`before each goal call X` is heard for the goals of your app, not for plang's own. The goals under `/system/` (the builder and the shortcuts) run when your program runs, and an event of yours does not hear them. The same holds for `each step` and `each action`: a step or action of a plang goal does not trigger an event of yours, so `before action file.read call X` does not run for the file reads plang's own goals make. An event bound on one named item (`before goal /system/error/show call X`) is heard.

Run: a program that binds `before each goal, call Log`, calls its own goal `Mine`, and then calls the system goal `/system/shortcut/Goal`; `Log` records the name of the goal it heard. After both calls it had recorded `Mine`, the last goal it was called for (the test `EachGoalIsTheAppsOwn`, 1 of 1 pass). Read: `PLang/app/event/this.cs:47–53`, `PLang/app/event/on/side.cs:53–58`.

The setting `event.setting.goal` is `app` (the default) or `all`. With `all`, events of yours hear plang's own goals too (`PLang/app/event/setting/this.cs`). It is read each time an event is about to run, so changing it in a run is heard at the next one.

## A name or a folder

`before each goal in api/*, call CheckIdentity` binds to the goals under the `api/` folder beside the goal whose step says it; `in /api/*` is the goals under `api/` from the app's root. A goal added there later is heard too. A name with `..` is refused (`PLang/app/goal/list/this.cs:210`, `GoalNameClimbs`). Read: `PLang/app/goal/pattern/this.cs:13–19`.

## A variable

`after %count% is set, call Log` runs `Log` after `count` is set. `%count.on…%` is the moments of the value `count` holds, not of the variable. Read: `os/system/modules/on/event.notes.md`, `PLang/app/type/item/variable/this.cs:105–117`.

## In a sub app

A sub app your program makes takes in your events, unless the step says `not in sub apps` (`Sub=none`). Your events run first in the sub app, and the sub app's own events come after; its own cannot take a failure your event raised (`PLang/app/event/list/this.cs:164–195`, `PLang/app/event/this.cs:55–58`). A sub app cannot take your event off (`EventNotYours`).

## Events when the app is ready

An app's events load the first time something asks for them, not at its start (`PLang/app/event/list/this.cs:5–17`). The files in `system/event/` are plang's own events and load then; `event.setting.system` is `load` (the default) or `skip`. An app's own `system/event/Hello.goal` replaces plang's. If one of those files fails to load, the ask fails with its error and nothing runs with half of them loaded (`PLang/app/event/list/this.cs:187–194`).

## Remove an event

`on.unbind` removes one. Keep what the event step answers to unbind it later (`os/system/modules/on/unbind.description.md`). Not run here.
