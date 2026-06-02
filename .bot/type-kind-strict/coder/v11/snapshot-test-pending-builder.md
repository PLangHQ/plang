# Snapshot fix-and-resume `.test.goal` — ready, blocked on the builder planner

The snapshot fix-and-replay loop is **code-complete and C#-proven** (see
`PLang.Tests/App/SnapshotTests/SnapshotWireTests.cs` —
`NavigateAndEditCapturedVariable_ThenResumeToSuccess`,
`MidStackChain_SurvivesDisk_ResumesDeep_AndUnwindsToEntryGoal`, etc.). The PLang
`.test.goal` below is the surface demonstration, but it **cannot be built on the
`type-kind-strict` branch** — `plang build` fails the planner (`BuilderPlannerFailed`)
even on the trivial `Start` goal, the same degraded-builder state as the
event-compilation and `CompileLlm_Kernel` issues. This is issue #1 (routed to the
builder), not the snapshot code.

## The test goal (build + run once the planner works)

`Tests/Snapshot/FixAndResume/Start.test.goal`:

```
Start
- set %x% = 1
- set %keepA% = 'alpha'
- call Check, on error call FixAndResume
- assert %reached% equals 'end'
- assert %keepA% equals 'alpha'

Check
- assert %x% is not 1
- set %reached% = 'end'

FixAndResume
- save %!error.callback% to file 'crash.snapshot'
- read 'crash.snapshot', write to %snap% as snapshot
- set %snap.variables.x% = 2
- resume %snap%
```

Flow: `Check`'s `assert %x% is not 1` throws (x==1) → `on error` runs
`FixAndResume` → it writes the throw-time snapshot to disk, reads it back as a
`snapshot` value, edits the captured `%x%` to 2, and `resume`s it → the assert
re-passes with x==2 and the chain completes. The single conditional-throw
(`assert`) is deliberate: resume re-enters the throwing step, so the gate must
re-evaluate on replay (an unconditional `error.throw` would re-throw).

## Builder bug surfaced (for the builder bot)

On the one run that did build (before simplification), `write %!error.callback% to
'crash.snapshot'` mis-mapped to **`output.write`** with `channel='crash.snapshot'`
— treating the file path as a channel name — instead of `file.save`. The
disambiguator is the `to file` keyword (`save %x% to file 'X'` → `file.save`). The
catalog/planner should not pick `output.write` for `write %x% to '<path-with-extension>'`.

## What IS committed on `type-kind-strict`

- The full snapshot serializer (format-agnostic leaf-serializers), `resume` verb
  (`app/module/snapshot/resume.cs`) + its catalog markdown
  (`os/system/modules/snapshot/`), the `Data<snapshot>` conversion seam, and the
  navigate+edit layer (`%snap.variables.x%` → owner `Get`/`SetVariable`).
- C# tests proving the end-to-end loop deterministically.

Once the builder planner is healthy, building + running this `.test.goal` should
go green and demonstrate the loop at the PLang level. (And under the App-as-snapshot
reframe, it becomes `%!app%` / `%app.Variables.x%` / `run %app%`.)
