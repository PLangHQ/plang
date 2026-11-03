# action-event-path — builder measurement (fresh builds, cache skip, scratch apps; sa1 = EvA..EvL, mark = doc/events.md Mark example)

Wordings (all `call` a goal; EvB, EvC and Mark also have `write out` steps after the bind):
A `before action output.write call Mark` · B same + `write out "one"` · C `before action output.write call Count` + write out · D `after action output.write call CaptureWrite` ·
E `before action file.read call Count` · F `before action http.get call Count` · G `after action output.write call Count` · H `before action file.read call OverrideFileRead` ·
I `before action output.write, call Mark` · J `after step call Count` · K `before each goal call Count` · L `before the app starts, call Say`

Right = Event is the action's own start (`%!app.module.<m>.<a>.on.start%`).

| | before (runtime2 binary) | after1 (rule first, varied examples) | after2/4 (+ action examples first) | after5 (final: + description names a named action first) |
|---|---|---|---|---|
| A-I x3 (27 builds), action's start | 6/27 (D 3, H 3) | 3 or 6/27 | 16/27 and 13/27 | 17/27 (A3 B1 C0 D3 E3 F3 G1 H3 I0) |
| Mark x5 | 0/5 (step 5/5) | - | 1/5 | 3/5 |
| J `after step`: step.on.start | 1/3 | 3/3 | 3/3 and 2/3 | 3/3 |
| K `before each goal`: goal.on.start | 0/3 | 1/3 | 1/3 and 1/3 | 0/3 (before: 0/3 too) |
| L `before the app starts, call Say`: app.on.start | 3/3 | 3/3 | 3/3 | 3/3 |

Misses are mostly `%!app.type.step.on.start%`; also `%!app.output.write.on.start%` (no `module.`) and, with a comma, `%!app.on.start%`.
Tried and dropped: naming the wrong forms in the notes (after3: 14/27). Raw lines: measure-*.txt.
Not reached: "every action wording builds the action's moment". It is 17/27, not 27/27; steps that are followed by `write out` (B, C, Mark) miss most.
K (`before each goal call Count`) was already 0/3 before this branch: the writer copies an example only when the called name matches too (same cause); not changed here.

Mark example (doc/events.md): last build binds `%!app.module.output.write.on.start%`; running it prints `one`, `two`, `marks: 3`. 3 of 5 builds bind output.write's start.

Moved pin: PickListTests TheStageOneRequest_IsThePinnedOne (re-pinned via AcceptTheFixture, word diff = only the `e.g.` lines of the `on` module's examples:
`after action output.write call CaptureWrite` -> `after action output.write, call Record`, `before action http.get, call Throttle`, `before action variable.set call Trace`).
Green: PickListTests 8/8 (after re-pin), ConfirmTemplateTests 2/2, LineTwinTests 1/1, PickOptionTests 18/18, ModulePageTests 8/8, TypePageTests 10/10. plang --test: 322 pass, 0 fail.
