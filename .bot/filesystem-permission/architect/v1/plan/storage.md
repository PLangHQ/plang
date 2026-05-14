# Storage — Deep Dive

## Where grants live

In the app's **system variables**, not in a new on-disk file layout.

Each app has a System actor (`app.System`) with its own variable scope. The list of signed `Data<Permission>` grants lives there as a variable. `Permission/@this` is a typed view over that variable — `List()` reads it, `Add(...)` writes it back, the variables system handles persistence and signing-envelope storage.

This is the right choice because:

- **No new format to define, version, migrate.** Variables are already structured, persistable, snapshotable.
- **Existing tooling works.** Audit = inspect the variable. Backup = whatever backs the actor's variables. Revoke = edit the variable.
- **The `Data<Permission>` envelope is already what variables hold** — signed Data is the universal payload across PLang. Storing grants as Data of Permission is consistent.

## What's still unpinned

The exact variable name and API to read/write it. Stage 2 confirms both by reading the variables system source. Candidates:

- `filesystem.permission` — scoped under the FileSystem subsystem
- `system.permission` — at the top level of the System actor
- `permission` — bare name

I'd lean `filesystem.permission` (dot-pathed, namespaced under the subsystem it serves) but it depends on conventions in the existing variables code. Reading `PLang/App/Variables/` and the system-actor wiring in Stage 2 is what pins this.

## Read path

```
Permission/@this.List()
  → systemActor.Context.Variables.Get("filesystem.permission")
  → unwrap as List<Data<Permission>>
  → for each: .Value (the Permission record)
  → return IEnumerable<Permission>
```

The Data envelope's signature is verified *somewhere* — either by the variables system when it loads, or by Permission/@this on each read. Stage 2 picks the layer. (Strong preference: variables system handles it once at load; Permission trusts the unwrapped envelope.)

## Write path

```
Permission/@this.Add(Data<Permission> signed)
  → systemActor.Context.Variables.Get("filesystem.permission")
  → append the signed envelope
  → write back through the variables system (persistence handled there)
```

`Add` doesn't sign — by the time we receive `Data<Permission>` it's already signed (PLang plumbing produced it from the user's prompt response). Permission just stores what it's given.

## Per-process vs. persisted grants

The current code distinguishes "y" (this process only — `ProcessId` set, `expires` null) from "always" (persisted forever). The new design's analogue:

- **Persisted grant.** Standard path — signed Data written to the system variable. Survives restart.
- **Process-only grant.** Same Data type, but kept in an in-memory list on Permission/@this only. Not written to the variable. Discarded at process exit.

Whether we want to keep the process-only mode at all is a small open question — if grants are always signed and always persisted, the "y, this once" UX becomes "always" with an immediate revoke. I'd lean: keep process-only as an explicit code path because the user's intent ("don't remember this") is real consent information.

## The lazy-vs-eager load question

When does Permission/@this read the variable?

- **Eager** — at construction (typically app startup). One read, fast lookups forever, predictable.
- **Lazy** — on first `Check` call. No startup cost, first call slower.

Variables that are persisted across restarts probably get loaded on actor construction anyway, so "eager from Permission's perspective" likely costs nothing. Lean eager unless the variables system has a cost model that argues otherwise.

## What the variable looks like

```
%filesystem.permission% =
[
  { "data": { ...signed Data envelope... }, "value": {
      "appId": "<messages-id>",
      "path": "/apps/*/system.sqlite",
      "match": "glob",
      "verb": { "read": {...}, "write": {...}, "delete": {...} }
  } },
  ...
]
```

The exact serialization is whatever variables already use for `List<Data<T>>` — we don't invent it.
