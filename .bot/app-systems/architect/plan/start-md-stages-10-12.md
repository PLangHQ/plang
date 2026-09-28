# Draft for `test/plan/app-systems/start.md` (decision 211, Ingi: "yes")

The coder places this: it replaces the "Done" line and the stage 10, 11 and 12 paragraphs. The rest of start.md stays. Then `start.goal` gets one goal per sub-stage (`Stage10a` … `Stage12b`), each with its behaviours as comments (intent). The tests come at each sub-stage's end, as before.

---

Replace the line under `## Done` with:

```
Stages 1–8h and 9a. See [done.list](done.list).
```

Replace the paragraphs for 10, 11 and 12 with:

```
**10: the app knows its types.** Every type a program can use is listed at `%!app.type.list%`, and a type is the plang value it says it is.
- **10a: registration.** Built-in types and a plugin's types join `%!app.type.list%`; one name can't be taken twice (a clash fails loudly). `%!app.list%` is kept for the apps running under this app, later.
- **10b: typed lists.** `list<text>` is made by the type itself: a kind carries its element. A list read no longer copies itself.
- **10c: the app's facts are plang values.** `%!app.created%` is a datetime, `uptime` a duration; a channel's settings are plang values. The app's identity is read back through the same format that writes it, the store is ready when the app is, and `id`, `name` and `environment` are the app's settings.
- **10d: formats live with their owners.** A type's formats are its kinds; json's writer and reader live with the json kind, text's with text, the step notation with the action. The test report's format is a real format kind (json, junit), and each writes its own report file.
- **10e: one form of computed value.** A value computed on each read (`%Now%`, `%!event%`) has one form.
- *Decisions:* the store is born ready; `code`, `clr` and `table` live under `app.type.item`; the app's identity is read through the same format that writes it; `%!app.type.list%` lists the types (Ingi); the format machinery moves to its owners, and test's format enum dissolves into format kinds (Ingi).

**11: one door per thing.** Each thing is reached one way.
- **11a: the actor is reached one way.** `%!app.actor.system%` and `%!app.actor.user%`; no second `app.System`/`app.User`. A shortcut, if ever wanted, is a goal that returns the value, not a second property (Ingi).
- **11b: tests reach the app through its own doors.** The C# tests start actions the way plang does, not through test-only helpers.
- **11c: nothing unreached goes unnoticed.** The builder warns about goals nothing calls; the test report shows what the tests reached.

**12: mistakes you can catch precisely.** A programmer's mistake is an error with its own key (`on error key "CannotSet"`), never a generic crash.
- **12a: the exception pass.** Every `throw` in the code this branch touched is either plang itself broken, or becomes an error in the result.
- **12b: born knowing.** A goal knows where it was loaded from when it's born, not stamped on after.
```

In `## What tests can't show`, rename `**11**` to `**11b–c**` (its two checks are 11b's and 11c's).
