# Code from elsewhere

For someone deciding whether to run an app or a goal they did not write. The question this page answers: what can it do to me, and what stops it?

## Running an app means trusting its code

The permission question ([files and permissions](files-and-permissions.md)) guards what a goal does as the User actor. It does not guard the app from itself. An app can:

- run a goal as the System actor, which is not asked;
- wire its own input channel, so it answers its own questions.

The design doc states it: "running an app means trusting its code; the prompts protect the user from what the User actor would do unasked, not from the app itself" (`Documentation/v0.2/filesystem-permission.md`). This was read, not run.

Before you run an app you did not write, read its `.goal` files. Look for the things these pages describe: a path that starts with `//` (outside the folder), a step that calls an address or asks a model (it sends data out), a step that exports an identity, a step that writes a key variable with `write out`, and a goal called as another actor. This list is what the runs here touched, not a complete list of what a goal can do. They are plain text. Do not run its `.pr` files without the `.goal` beside them: a `.pr` is what the builder wrote, and it is not meant to be read by hand.

## A sub app

An app can make a sub app. A sub app:

- is made in its maker's folder or under it, never in its maker's `.data/` folder. A breach answers `OutsideItsMaker (400)` at birth (`PLang/app/actor/this.cs:177-184`). A sub app rooted in its maker's stores would read the maker's keys as its own.
- never owns the terminal: it writes to the console and reads an input that is at end of file (`PLang/app/actor/this.cs:188-190`), so a permission question inside it has no one to answer it.
- reads its maker's types, modules and goals through layers and never writes them. What it adds goes to its own layer.

`SubAppRootTests` (4 tests) pass; `RoleStandingProbeTests` (18), `StoreStandingProbeTests` (3) pass. The layering rule's guard is `PLang.Tests/Types/App/Types/SharedEntryWritesTests.cs` (named in `Documentation/v0.2/apps.md`, not run for this page).

## What a goal cannot do on its own

As seen in the runs for [files and permissions](files-and-permissions.md): a goal run as the User actor, with no one to answer, is refused any path outside its folder, and is refused the app's `.data/`. It cannot export a private key.

## What was not tried

- Loading and running a goal from another author's folder as a stranger.
- A sub app made from a goal and what it can reach in practice.
- A network goal or an `http` step inside a sub app.
