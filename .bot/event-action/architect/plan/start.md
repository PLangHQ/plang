# event-action

Cut 2026-10-29 from runtime2 6cfe782d7 (snapshot's merge). Ingi: "lets do the event-action". Replaces `.bot/snapshot/architect/plan/branch-event-action.md`, which went stale during snapshot (245, 248, 251, 271 came after it). The coder places this plan at `test/plan/event-action/`.

## Why

What runs when an event fires is the event's action, not a "binding" (Ingi, snapshot 234: "I think the 'binding' is not needed in there", "it should be removed the 'binding'"). Since snapshot 10a a binding has a member called `action` (the on.event step that made it), so `event/binding/action` reads as "the binding's action" while it names a kind.

Snapshot also decided pieces of the event system that it didn't build, leaving them to this branch: reaching an event by path from `app.event` (244, 245), each binding choosing whether it reaches sub apps (247–251), `on.before`/`on.after` (248), and two older findings (230's 7 and 8): the base class doubling as the C# handler, and ten event classes that differ only by name.

```
- before each goal, call Log
- after each file.read, call Audit, not in sub apps
- write out %!app.event.goal.before%
```

**Today:** the first step builds to `Event=%!app.type.goal.on.start%, When=before` and answers a *binding*. Every binding reaches every sub app born after it; there's no way to say "not in sub apps". `%!app.event.goal…%` doesn't navigate: `app.event` only lists what a program bound.

**After:** `Event=%!app.event.goal.start%, When=before`, answering the event's *action*. `Sub=none` keeps the second out of sub apps. The third step prints the actions that run before each goal.

## Ingi's decisions (traced in `.bot/snapshot/architect/summary.md`)

- **234:** the layout `event/action/{this, Scope, list, call, clause}`; plang's word changes from binding to action; `on.unbind` stays (unbinding is done to an event). 230's items 7 and 8 come with it.
- **244, 245:** `app.event` is the app's event system, reached by path, each segment a member of the one before. `start` is the default event: `app.event.goal.before` (Ingi: "what is the first .start? goal/step/action are only before and after, right?"). Goal, step, app and action have `before` and `after`; a goal also has `load`. "Level" is dropped as a word (Ingi: "dont understand level").
- **247, 249, 251:** each binding chooses whether it reaches sub apps; by default it does (Ingi: "by default it does, but settable"). The choice is `Sub: inherit | none` (Ingi: "yes, looks fine on the choice").
- **248:** `on.before` and `on.after` (the start event's sides) are built here.
- **265 and security:** a handler inherited into a sub app calls the sub app's goals, so policy that must hold in a sub app isn't built on an inherited handler's calls; enforce in `before`, an `after` observes. Both go in on.event's notes.
- **269, 271:** `on.Declared` and the `declared` event class go to this branch's member pass.

## Stages

1. **The move and the word.** `event/binding/**` becomes `event/action/**`: `this.cs` (base), `Scope.cs`, `list/` (before, after), `call/` (was `binding/action`, what on.event binds: it runs its call), `clause/` (on.error, on.cache, on.timeout), `inherited/`. The app's list moves from `event/on/list/` to `event/list/` (`app.X` is typed `X.list.@this`). on.event answers `data<event.action.@this>`; on.unbind's parameter `Binding` becomes `Action`. Error keys and messages that say binding say action (the coder lists them, I rule). The teaching in `os/system/modules/on/*` and `loop/*` (check each: the loop files may use the word for an item) changes, so the decider fixtures and page goldens re-pin; the surface golden re-pins under this decision. The coder's saved patch (`.bot/snapshot/coder/event-action.patch`) no longer applies (`binding/inherited/` came after it), so the move is redone. Boy scout (snapshot 39): each action kind holds an actor, so it clones as itself.
2. **`app.event` by path.** `%!app.event.<path>%` is the events of what `%!app.<path>%` reaches. A collection node stands for each of its elements: `app.event.goal` is where "each goal" is heard (today `app.goal.type`), `app.event.goal["/Process"]` is that goal's, `app.event.module.file.read` is each file.read's, `app.event.type.step` each step's. Under it, `before`/`after` are the start event's sides (`on.before`, `on.after`), and `load`, `error`, … the named events. on.event's `Event` is reached this way; its examples move from `%!app.type.goal.on.start%` to `%!app.event.goal.start%`. One door to bind: the coder traces how `…on.start` navigates through an item today and proposes how it goes. A read makes nothing: navigating to an item nothing is bound on adds nothing to `app.event`. 13 built files and 3 test goals use the old path; the builder rebuilds them after the teaching lands.
3. **`Sub: inherit | none`.** A choice on on.event, `inherit` the default, held on the action; the sub app's birth takes in only the actions that reach (`action.list.Inherit`). A snapshot keeps it with the action. The notes get 265's rule and security's. A new on.event property re-pins PickListTests, and ModulePageTests if the notes change.
4. **The C# action is its own kind; one event class.** NEW `event/action/code/`: the C# handler (debug's watch and its setting's, the test runner's timing, coverage, mocks). The base keeps only what every action shares (actor, scope, filter, reach, `Start`, `Remove`); `_handler`, `_handler!` and the two-constructor split go. The ten event classes that differ only by name (`start`, `load`, `create`, `set`, `remove`, `write`, `read`, `ask`, `hit`, `miss`) and `declared` become the event class itself; `error` stays a subclass (it has `Catch`). `on.Made` goes; on's typed members answer the event. `on.Declared` gets its name in the member pass.
5. **"Level" goes as a word** (245). The places an item's event is heard (each goal, then this goal; an action has four) are still walked in one pass (254: one door, so the stop on a failure or Handled lives once). `item.Level(depth, context)` is a locked base virtual, so its new name is Ingi's.

**Boy scout, in files this branch moves** (from the backlog, snapshot 341, 344): the app's events are found by item in O(n) (`on/list/this.cs:36–44`); `on.unbind` leaves an emptied `on` in the list; a resume keeps one of two actions the same on.event step made (`event/this.cs:89`, the dedupe by action). The coder proposes each fix.

## Demolition

Dies in this branch:
- [ ] `event/binding/**`: moved, nothing kept beside it.
- [ ] `binding.@this._handler`, its handler constructor, and `Handle`'s `_handler!` (`binding/this.cs:13, 16–21, 40–42`) → `event/action/code/`.
- [ ] `on.Made` (`on/this.cs:15–29`) and the classes `on/{start, load, create, set, remove, write, read, ask, hit, miss, declared}.cs`; event's `abstract` and its `private protected` constructor.
- [ ] on's casts (`(start)this["start"]!`, `on/this.cs:85–109`).
- [ ] `event/on/list/` → `event/list/`.
- [ ] The old path in prose: on.event's doc comment (`module/on/event.cs:5–6`), `on.NoEvent`'s message (`on/this.cs:116`), the event reader's refusal (`serializer/Reader.cs:19–21`).
- [ ] The word `Level` (stage 5, after Ingi names it).

Stays (don't delete):
- `item.on`: an item's own events (an action's clauses bind there) and its class's shared, closed ones.
- `When` (before | after), `Scope` (actor | app), the `error` event class, the verb `on.unbind`.
- The one-pass walk over the places (254).
- `IsBound` and its callers (on the backlog, not this branch).

## Proof (`start.goal`)

Each test goes red without its stage's change.

1. on.event answers an action, and `on.unbind Action=%a%` takes it off. The surface golden and the decider fixtures re-pinned under this decision.
2. "before each goal, call Log" binds through `app.event.goal.start`, and Log runs before each goal. `%!app.event.goal.before%` holds one action after the bind and none before. `app.event.goal["/Process"].start` fires for Process only. `after each file.read` fires after a file.read. Reading `%!app.event.goal.before%` in an app that bound nothing leaves `%!app.event%` empty.
3. A binding with `Sub=none` doesn't fire in a sub app; one without it does; a snapshot keeps `Sub` and a resume honours it.
4. The debug watch, the test runner's timing, coverage and a mock each still fire, and none is saved in a snapshot. Every core event except `error` is the event class; a type's declared event (`[Events("closed")]`) binds and fires.
5. Stage 5 is a rename: the surface golden only.

Boy scout pins: an emptied `on` leaves the list after unbind; two actions from one on.event step both fire after a resume.

## OBP check

| pattern / smell | where | resolution |
|---|---|---|
| a compound name is a dot path | `binding/action` | `event/action/call`: an event's action, of the kind that runs a call |
| a type per kind, decided at birth | the base doubling as the C# handler | `code` is its own kind beside `call`, `clause`, `inherited`; the base has no handler |
| a type per kind (inverse) | ten classes that differ only by name | one event class; `error` stays a kind because it behaves differently |
| a choice belongs on the element | reach | `Sub` on the action, not an app setting: whoever binds knows (247) |
| one door | binding through `…on.start` and through `app.event` | `app.event` only (stage 2's trace) |
| a read makes nothing | navigating `app.event.<path>` | a read never adds an item's events to the list |
| names | `Sub`, `inherit`, `none`, `code` | single words; `code` is plang's word for C# behind a door (`[Code]`) |

## For the coder

Push back on anything. Stage 2's navigation shape (what `app.event.goal` answers before anything is bound, and how on.event finds the item from it) comes to me before code. A new member on a locked surface comes to me first; I take it to Ingi.
