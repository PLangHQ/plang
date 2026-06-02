# Snapshot fix-and-resume: root cause is the builder dropping `as snapshot`

The snapshot/resume **runtime is correct and fully C#-proven** (no live LLM). The
`Tests/Snapshot/FixAndResume/Start.test.goal` failure is a **builder cast-drop**,
not a runtime bug.

## Proof (deterministic C#, `PLang.Tests/App/SnapshotTests/SnapshotWireTests.cs`)

- `ThrowTimeSnapshot_EditSurvivesResume` — snapshot via `App.Snapshot(error)`
  (the `Error.Callback` path: `SnapshotAt` + `error.CallFrames`) → disk → edit
  `%x%`=2 → resume → **seen == 2**. PASS.
- `TypedSnapshotString_NavigateEditResume_PersistsEdit` — `%snap%` = a Data whose
  **Value is the raw wire STRING off disk** but whose **Type is `snapshot`** →
  navigate `%snap.variables.x%` → edit → resume → **seen == 2**. PASS.

So: when the variable is *typed* `snapshot`, navigate→edit→resume persists. The
runtime materialises+caches the snapshot on navigation. Nothing to fix runtime-side.

## The bug — in the compiled `.pr`

`Start.test.goal` line: `read 'crash.snapshot', write to %snap% as snapshot`

Compiles to (FixAndResume, step 2):

```
file.read   path='crash.snapshot' ResolveVariables=False
variable.set Name=%snap%  Value=%!data%  type={ name: "object" }   ← WRONG
```

The `as snapshot` clause is **dropped**: the `variable.set` Value param is typed
`object`, so `%snap%` holds the raw wire **string**. Consequence:

- `set %snap.variables.x% = 2` converts the string → a *throwaway* snapshot,
  edits that, discards it. `%snap%` is unchanged bytes.
- `resume %snap%` re-converts the original bytes fresh (x=1) via `As<snapshot>`.
- Check re-fails: x is still 1.

## The fix (builder domain)

`write to %VAR% as <TYPE>` must stamp the **target variable's type** to `<TYPE>`.
The compiled `variable.set` Value param (or the target Variable) should carry
`type={ name: "snapshot" }`, not `object`. With that, `TypedSnapshotString...`
above is exactly the runtime path and the `.test.goal` goes green unchanged.

Earlier builder note (still valid): `write %!error.callback% to file 'X.snapshot'`
must map to `file.save` (it now does — step 1 compiles correctly to `file.save`).

## Status

Runtime + tests committed/pushed on `type-kind-strict`. The `.test.goal` is in
place (reverted to clean, no probes). It will pass once `as <type>` types the
write target.
