# FreshAppGoesOn

`FreshAppGoesOn.test.goal` runs `Record.goal` (Record calls Sub, Sub saves the run), which keeps the snapshot in
`workspace/run.plang`. The test reads that file back and resumes it, and checks the run goes on from its save: the
context's `%name%`, Sub's parameter `%city%`, Record going on after the call, and Sub's step before the save not running
again.

The save signs the snapshot with the app's identity, and a resume takes only a snapshot one of the app's own identities
signed. A test app's identities live in memory, so the file is written and resumed in the same run; a committed file
would be another app's. That a fresh app sharing the identity goes on from it is proven in C#
(`SignedSnapshotTests.AFreshApp_GoesOn_FromTheFirstAppsSignedPlang`).
