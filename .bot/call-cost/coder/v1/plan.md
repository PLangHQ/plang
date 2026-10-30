# coder — call-cost v1: stage 1, made on the first ask (pattern 11)

From `.bot/call-cost/architect/plan/start.md` stage 1 and the architect's brief.

1. The step cache (`app/this.cs:199`): `Cache => _cache.Value`, a `Lazy<ICache>` made at its first ask (on.cache, the
   signing nonce), as the app's `store` already is; Dispose lets go only one that was made. The unused `internal set`
   goes. The public member and its type stay (the app's surface is locked).
2. A frame's own variable store (`call/this.cs`, `_parameter`), its `Tags` and its `Errors`: each null until its first
   ask, then made once (`Interlocked.CompareExchange`, parallel branches share it). Reads that find nothing made answer
   as before with nothing made: `TryGet`, `Holds`, `Names`, `Error`, `Record`'s duplicate check. No member added.
3. `goal.Broken()`: judged at the goal's first start and kept (a loaded goal's steps don't change; a cache that gives
   every app the same answer).

Pins (C#, `PLang.Tests/Runtime/App/Context/BirthTests.cs`), each red without its change:
- a sub app's birth (the step cache);
- a frame pushed and popped with no write;
- a goal started again.

Not mine: the bench (`Tools/Bench/run.sh`) is the codeanalyzer's (Ingi, 2026-10-25: "you are a coder you don't need
to do this, code analyzer at the end should").
