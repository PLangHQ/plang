# FreshAppGoesOn

`FreshAppGoesOn.test.goal` runs `Record.goal` (Record calls Sub, Sub saves the run), which keeps the snapshot in
`workspace/run.plang`. The test reads that file back and resumes it, and checks the run goes on from its save: the
context's `%name%`, Sub's parameter `%city%`, Record going on after the call, and Sub's step before the save not running
again.

The save signs the snapshot with the app's identity, and a resume takes only a snapshot one of the app's own identities
signed.

**Known red until stage 7:** under `plang --test` the run sits under the test runner's goals, and the call stack is
saved as a nested tree (about four levels per frame), so the snapshot is deeper than the 64 levels plang's readers read.
The save refuses it (`ValueTooDeep`). Stage 7 saves the stack as a chain, one entry per goal call, which brings it back
under the limit.

A test app's identities live in memory, so the file is written and resumed in the same run; a committed file
would be another app's. That a fresh app sharing the identity goes on from it is proven in C#
(`SignedSnapshotTests.AFreshApp_GoesOn_FromTheFirstAppsSignedPlang`).
