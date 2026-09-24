# Debug watch: a list of names, the store's own events

With Ingi, 2026-09-24. Approved ("no need to have extra objects in the runtime"). Queued after the error fallback.

> **Coder, you own this.** The code shape is yours; the rulings are fixed.

## Why

- `--debug={"variables":[{"name":"trace","event":"onchange"}]}` fails: "setting 'variables' cannot bind to List<DebugVariable>". The setting walk (`setting/this.cs:93-124`) descends a nested dict into a composite, but a list of composites is excluded (`:134`, any `IEnumerable`), so it takes the leaf path and `list.Clr(List<DebugVariable>)` can't turn dicts into a plain C# class.
- `DebugVariable` (`module/action/debug/this.cs:689-693`) and `DebugEvent` (`:687`) exist only to pair a name with an event. The watch plants a NotFound placeholder Data per name carrying handlers (`Activate`, `:126-145`), relying on the store to carry them onto the real variable.
- The variable store already announces every change for any name: `OnSet` (name, before, after), `OnCreate` (name, value), `OnRemove` (name) (`variable/list/this.cs:60-70`); the call frame's diff capture uses them (`callstack/call/this.cs:148-149`).

## Rulings (Ingi)

1. `Debug.Variables` is `list<text>`, names only: `--debug={"variables":["trace","goal"]}`. It binds through the existing walk (as `app.Test.Include`/`Exclude` do); no setting-walk change.
2. Debug subscribes to the User actor's store `OnCreate`/`OnSet`/`OnRemove` and logs created/changed/deleted for the watched names. Watched names still print at each step (`:560-563`).
3. No event per name: a watched variable always shows its creates, changes and deletes.

## Rulings after coder's trace (Ingi, 2026-09-24)

4. **Binding.** My plan was wrong: a `list<text>` setting did not bind (`Include`/`Exclude` failed the same way; `TestingClassTests.Configure_FromJson_*` were red). The setting walk (`setting/this.cs:110-114`) builds a plain list and asks it for `list<text>` via `Clr`. Fix (coder's option b): the list converts itself; a base list asked for `list<T>` moves its rows into the typed list. The walk doesn't change. Ingi: "I just want to be able to send in those variables and the plang list<text> so it can be used."
5. **The per-Data event lists go** (`Data.OnCreate`/`OnChange`/`OnDelete`, `data/this.cs:72-82`, their Fire* and all copying). Once the watch listens to the store, nothing adds to them. The store's own `OnSet`/`OnCreate`/`OnRemove` stay.
6. **For later:** Ingi plans `- on %name% create, call NameCreated`. That builds on the store's events, which announce a name before any value exists. They hand out `.Peek()` values (`variable/list/this.cs:156-159, :182-185`); a plang event should receive the Data. Change it when that feature is built.

## Demolition

| Dies | Note |
|---|---|
| `DebugVariable` | |
| `DebugEvent` (`OnTypeChange` was `OnChange` with a filter) | |
| the NotFound placeholder planting in `Activate` | and the `%` stripping of `DebugVariable.Name`; strip on the text names if still needed |

**Check, then bring back (don't delete on your own):** the per-Data event lists (`OnCreate`/`OnChange`/`OnDelete`) that the store copies onto a rebound Data (`variable/list/this.cs:146-171`) are documented as existing for this watch (`Documentation/v0.2/data-internals.md:213`, `SubscriberSurvivalTests.DebugWatch_OnChange_FiresOnEveryReplacement`). List what else uses them.

**Docs:** `Documentation/v0.2/debug.md:22`, `build.md:54`, `data-internals.md:213`, `data-generic-design.md:618` state the new flag shape.

## OBP validation

| Surface | Check | Result |
|---|---|---|
| `Debug.Variables` | plang type, one word | `list<text>`, no CLR record |
| watch mechanism | one door per question | the store's own events; no planted placeholder |
| Debug's other leaves | plang types? | `Goal` (string), `Step` (int?), `MaxLength` (int), `Verbose` (bool), `Grep` (string) are CLR: noted, not in this change |
