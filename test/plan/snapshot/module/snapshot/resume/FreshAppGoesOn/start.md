# FreshAppGoesOn

`FreshAppGoesOn.test.goal` runs `Record.goal` (Record calls Sub, Sub saves the run), which keeps the snapshot in
`workspace/run.plang`. The test reads that file back and resumes it. The run goes on in an app of its own, made by the
test's actor, so the test's own variables are never touched by it.

The resumed run goes on from its save: Sub's next step reads the context's `%name%` and its parameter `%city%`, Record
goes on after the call, and Sub's step before the save doesn't run again. Then it goes on through the test's own goal,
from the step after `call Record`. There it meets `workspace/resumed.marker`, written after the save and before the
resume, so the snapshot never holds it. `Resumed` writes what the run saw to `workspace/seen.txt` and the run ends, never
reaching its own `resume` step again. The test reads `seen.txt`, and checks that its own `%seen%` and `%count%` are still
what it set.

The save signs the snapshot with the app's identity, and a resume takes only a snapshot one of the app's own identities
signed.

A test app's identities live in memory, so the file is written and resumed in the same run; a committed file
would be another app's. That a fresh app sharing the identity goes on from it is proven in C#
(`SignedSnapshotTests.AFreshApp_GoesOn_FromTheFirstAppsSignedPlang`).
