# Proposal: a snapshot is just the App, serialized — for architect review

**Status:** design proposal. The current `type-kind-strict` branch has a *working,
C#-proven* snapshot implementation (serialize → disk → restore → resume, including
mid-stack edit-then-resume-and-unwind). This proposal is the **end-state
architecture** that supersedes it. Posted on its own branch so the architect can
weigh in before we commit to the reframe.

## The realization

`snapshot.@this` is a parallel structure that **mirrors the App**. Its sections are
named `Variables`, `CallStack`, `Errors`, `Build`, `Testing`, `Providers`,
`Statics` — which is exactly the App's own restorable subsystems copied into a
second tree. A snapshot isn't a *thing*; it's **the App frozen at a step**.

"Snapshot" is a Noun standing in for "the app, serialized" — the same Noun+Verb
smell, one level up. Remove the noun and it collapses to:

```
- write %!app% to file '%filename%'   / serialize the app at its current state
- read '%filename%' into %app%         / deserialize back into an app instance
- set %app.Variables.x% = 2            / it's just the app's variables
- run %app%                            / run / resume it from where it was
```

No capture verb, no `snapshot.resume`, no `snapshot.@this`, no `Io`, no
`VariablesView`. `%snap%` was always going to be `%!app%`.

## What dissolves vs. becomes

| Dissolves | Becomes |
|---|---|
| `snapshot.@this` tree, `app/snapshot/Io.cs` | nothing — the App *is* the structure |
| `snapshot/serializer` + capture-into-a-tree | `App` is the wire-citizen; each subsystem still owns serializing its own slice (transfers) |
| `snapshot.resume` verb (`app/module/snapshot/`) | `run %app%` — `run` sees an App value and resumes it |
| `%!error.callback%` as `Data<snapshot>` | the App, frozen at the throw (`%!app%` at throw-time) |
| `%snap.variable.x%` special navigation + `VariablesView` | `%app.Variables.x%` — ordinary app navigation; the variables collection is navigable/settable as a general app capability |
| `Data.Snapshot` side-channel | gone (see the existing removal todo) |

## What transfers from the `type-kind-strict` work (so it isn't wasted)

The current branch proved the **mechanics** the App model will reuse:
- Each subsystem's `Capture`/`Restore` (`ISnapshot`) — that *is* the app serializing
  its restorable state. The reconstruct-on-build subsystems (Goals, Modules,
  Channels, Cache) are correctly excluded already.
- The **format-agnostic leaf-serializer** pattern (`Write(value, IWriter)`), so the
  App and its subsystems render themselves without naming a format.
- The **non-signing Store wire** (internal in-process state, not an actor-boundary
  crossing).
- The **CallStack-chain re-entry** (`Resume` walks the captured chain, re-enters the
  failing step, unwinds to the entry goal) — proven mid-stack, with the suspended
  variable edited and the edit flowing into resumed execution.
- The **conversion seam** (`TryConvert` honoring a type's `FromWire`) — reframes from
  `string → snapshot` to `string → App`.

## Open questions for the architect

1. **What does the deserialized `%app%` hold — a full second `App` instance, or a
   restorable-state value?** Ingi's picture: a full App instance, the same thing
   `%!app%` is. That makes `App` a serializable, navigable, *runnable* PLang value.
   Implications to resolve:
   - **Multi-App coexistence.** The live App is the root singleton (owns FileSystem,
     Channels, the actor tree). Can a *second* App exist as a `%app%` value? Or does
     `read … into %app%` produce a restorable-state object that `run` applies to the
     live App? This is the crux.
   - **Reconstruct-on-build subsystems.** The wire carries only restorable state
     (Variables/CallStack/…). A deserialized App still needs Goals/Modules/Channels —
     rebuilt from the current root, or re-pointed? Today `Restore` restores *into* a
     live App that already has them.
2. **`run %app%` semantics.** Does `run` restore the app-state into the current App
   and re-enter (today's `Resume`), or does it *become* the running App? The
   `run`/`callback.run` verb learns to resume an App value either way.
3. **`%!app%` as a serializable value.** Writing `%!app%` to a file must serialize the
   App's restorable state via the channel — i.e. `App` gets its own leaf-serializer
   (it owns its wire shape), composing the subsystem serializers. Confirm App is the
   right wire-citizen vs. an explicit "app state" projection.
4. **The variables collection becomes navigable + settable** (`%app.Variables.x%` read
   and `set`). This is a general app capability landing on `variable.list.@this`
   (which already owns `Get`/`Set` by name) — not a snapshot wrapper. Confirm the
   shape: make it `IDictionary<string,object?>`, or add navigate/set hooks that route
   to its existing `Get`/`Set` (preserving mint-`Data<T>` + event semantics).

## Suggested migration

1. Land App-level `Serialize` (App as wire-citizen, subsystems own their slices).
2. Make `read … into %app%` deserialize to the App value model chosen in Q1.
3. `run %app%` resumes (reuse the CallStack-chain re-entry).
4. Make `variable.list.@this` navigable + settable (Q4).
5. Delete `snapshot.@this`, `Io`, `snapshot/serializer`, `app/module/snapshot/`,
   `VariablesView` (already deleted), and the `Data.Snapshot` side-channel.
6. Re-point `%!error.callback%` to the app-at-throw.

The `type-kind-strict` snapshot implementation stays as the proven substrate /
reference until this lands.
