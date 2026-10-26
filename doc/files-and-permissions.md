# Files and permissions

For someone deciding whether to trust PLang. The question this page answers: what may a program read and write on my machine, and when does PLang ask me?

## Inside the app's folder

A program reads and writes freely inside its own folder, the app's root. In a goal, `/` is that folder. Nothing asks. A goal that read `person.json` and one that saved `cached.txt` into its folder ran with no question.

Two folders inside the root are not free:

- `.data/` holds the app's stores: its settings, saved grants and identities. A goal that reads from it is refused, and nobody is asked. In a run, `read .data/setting/data.sqlite` answered `PermissionDenied`, and no question was printed.
- The folder that ships with PLang (`os/`) is read by every actor and written only by the system. This is the design stated in `Documentation/v0.2/filesystem-permission.md`. `OsFolderGrantTests` (5 tests) pass; whether a user goal's write there is refused was not run.

## Outside the app's folder

A path that starts with `//` is the machine's own root. `//etc/hostname` is `/etc/hostname`. A program that touches such a path is asked first, through its input. The question is printed to its output:

```
Allow User to read //etc/hostname, which is /etc/hostname? (y/n/a)
```

Answers, as seen:

- `n`: the step fails with `PermissionDenied`. A step can handle it with `on error`; the run printed `denied: PermissionDenied` and went on.
- `y`: allowed. It was asked again on the next run (a write to `//tmp/plang-trust-out.txt` asked again), so a `y` is kept for that run only.
- `a`: allowed, and kept. On the next run the same read was not asked.
- No one answering (standard input empty, closed or redirected): the same `PermissionDenied`. Nothing is allowed because nobody was there to say no.

A grant is for the place asked about and the verb asked (read, write, delete). A grant for reading `//etc/hostname` did not stop the write to `//tmp/...` from being asked.

## The three homes of a grant

From the design doc (`Documentation/v0.2/filesystem-permission.md`), not run in full:

- **Standing:** what the actor's role stands on the app's own folders. In memory, never asked, never saved.
- **Once (`y`):** in memory until the app exits.
- **Always (`a`):** saved in the actor's permission setting, one signed row. A row that was tampered with reads as no grants.

The saved grant surviving a run was seen. Its signature, and the tamper behaviour, were not run here; `app/permission/grantastorespersisted` (a plang test) stores one and passes.

## What the question guards, and what it does not

The question guards what a goal does as the User actor. It does not guard the app's own code: an app can run a goal as the System actor, and can wire its own input channel so that it answers its own questions. The design doc says so itself: running an app means trusting its code. See [code from elsewhere](code-from-elsewhere.md).

## Tests

- `test/app/permission` (2 plang tests): an out-of-root save with an answering channel, and a persisted grant. Both pass.
- `StoreStandingProbeTests` (3), `OsFolderGrantTests` (5), `StandingTests` (2), `RoleStandingProbeTests` (18), `PermissionAllowsTests` (8) pass.

No test was found for the sentence "a `y` is kept for the run only". It was seen by running twice.
