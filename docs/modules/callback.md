# Callback Module

Go on with the run a callback holds.

A *callback* is a signed snapshot of where a run paused (`Documentation/v0.2/snapshots.md`). An error's `%!error.callback%` holds the run as it stood where the error happened, and a pending [`output.ask`](output.md#ask) carries one. In most goals you don't start callbacks yourself; the channel does it when an answer comes back from a paused ask. You write `- start %callback%` when you hold a callback from elsewhere.

## Actions

### start

Go on with the run the callback holds, through `snapshot.resume`, so a callback is checked as every resume is.

```plang
/ Read a kept callback, go on with its run
- read file 'pending-callback.plang', write to %callback%
- start %callback%, write to %result%
- write out 'Resumed: %result%'
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Callback | Data | yes | The signed snapshot a callback holds |

**Returns:** what the resumed run answers.

If anything goes wrong, you get a typed error back as `Data` — never a raw exception. The error keys you'll see are the resume's:

| Error key | Meaning |
|---|---|
| `SnapshotUnsigned` (403) | The callback has no signature |
| `SnapshotSignatureInvalid` (403) | It was changed after it was signed |
| `SnapshotNotTheApps` (403) | It was signed by an identity that is none of this app's |
| `NoSnapshot` (400) | The value holds no snapshot |
| `StepNotFound` / `ActionNotFound` (404) | The saved place is past the end of the goal's step or the step's action |
| `CallbackActionMismatch` (409) | An action read back from its saved key finds another action at its place (`goal/step/action/serializer/Snapshot.cs`) |

The signature check is unconditional: there is no "unsigned in-process" path.

A goal changed since the callback was made is resumed at its saved place; when another action stands there, a warning says so and the run goes on.

## See also

- [output](output.md) — `ask` is the main producer of callbacks.
- `Documentation/v0.2/snapshots.md` — what a snapshot holds and how a resume goes on.
