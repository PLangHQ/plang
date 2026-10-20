# tester — test-speed — summary

**Version:** v1 (playground; sections §6 – §24 in `test-speed.md`, newest first from §24)

**What this is.** A playground branch (decision 159), cut from item-doors (`b2354e747`), for app startup speed and
memory, aimed at a server that makes an app per request. Ingi opened the locked core for it: nothing merges from
here; the architect decides what to take. Every change keeps the C# sweep green but Wire's six `carried` snapshot
tests (red on the parent too) and `plang --test` on the same 11 failing names (diffed by name after every step).

**Where it ended.**
- A small app end to end (birth, load its goal, run, dispose), Release + server GC + shared types: Condition
  **~0.9 ms, ~100 KB** (was 6.1 ms, ~780 KB in Debug at the start of the small-app work).
- A bare request app's birth (shared types, no test session): **5.8 KB** (was ~90 ms and MBs at the very start).
- Suites against the parent commit: C# sweep **137 – 215 s → 29 – 31 s**, `plang --test` **24 – 31 s → 5 s**.

**The pieces, by weight (details and numbers per section in `test-speed.md`).**
- Code facts read once per process (`PLang/app/assembly/this.cs`), lazy everything at birth (§6 – §17).
- Goals read once per code and folder, shared by every app (`PLang/app/goal/program/this.cs`), §18.
- Class facts once per process (choice sets, properties, settings), lock-free list reads, settings/handler/path trims (§18 – §22).
- **One type list per code** for apps born with `shareTypes` (`assembly.types`, `type.list.IsShared`, refusals `TypesShared`), §23.
- **Bindings on shared objects held by the app's context** (`PLang/app/event/held/this.cs`; every event level reads
  its object's events and the app's holds), §19 / §23.

**Decisions open for Ingi / the architect.**
- Locked-core surface moves re-pinned on the playground: the app ctor's `assembly` and `shareTypes`; the path scheme
  classes; `type app.event.held`.
- A shared app can't load code (copy-on-write rejected: identity would split); `plang --test` decides it from the program.
- Throughput can't be measured on this shared machine (req/s swung 1.9k – 3.6k); a quiet machine is needed.
- Next structural lever if wanted: share the module catalog like the type list.

**Code example.**
```csharp
// a server: read plang's code once, then one app per request sharing its types
var code = new global::app.assembly.@this(typeof(global::app.@this).Assembly);
await using var app = new global::app.@this(root, assembly: code, shareTypes: true);
var goal = await app.goal.Load("/api/Start.goal");          // read once per process, shared by every app
var result = await ((global::app.goal.@this)(await goal.Value())!).Start(app.actor.list.User.Context);
```
