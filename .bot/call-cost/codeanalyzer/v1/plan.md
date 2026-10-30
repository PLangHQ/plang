# v1 plan — call-cost stage 1 (77add2895..4dcfa3f36)

Patterns first: 11 (lazy: a piece made by its first asker), 12 (one answer), and the return-early check. Scope: `app/this.cs`, `call/this.cs`, `goal/this.cs` (the diff's methods and the touched files' neighbours), `BirthTests`.
Bench: sub and root birth, sub and root request at both hashes (`run.sh`, birth + request, `LOAD_MAX=99`), and `repeat`/`phases` (a goal run again in one app) at both. No mutations, no production edits.
