# coder — app-systems

**Version:** v10 (stage 9: 9a closed, 9b under way)

## What this is
app-systems makes every `app.X` the type X, so the plang path, the C# path and the file path agree. The
architect's plan is `.bot/app-systems/architect/plan.md` (13 stages); its running log of rulings is
`.bot/app-systems/architect/summary.md`. The plan's readable face and its tests are `test/plan/app-systems/`
(`start.md`, `start.goal`, `done.list`). Stage 9 moves every value's birth onto its type (`type.Create`, which
fires `on.create`) and makes each module action a one-line door to the object that owns the work (worklist:
`.bot/app-systems/architect/plan/stage-9-worklist.md`).

Working mode (Ingi): the architect hands intent; the coder proposes and starts in the same turn, stops only at
a real design fork, and reports each pushed slice for review. Plan tests are intent: the coder writes them in
plang that compiles, keeping what they prove; each must fail with its change reverted.

## What was done (v10, all pushed)
- **9a — closed.** `data<T>.Use` (a carrier answers its own failure or hands its value whole);
  `data.Use<TAs>` (what a carrier holds, taken as held); file.read as the template action; `path.Read` is
  path's one read verb (a reference; content is the reference's own value; raw bytes via `IContent.Content`);
  reads and `as <type>` conversions are births; a type declares what it is born `From`; Fluid includes and
  OpenAi images read async; the file and http channels are gone; `data.Follow` (a reference answers the Data
  it names, unread); a value already of the declared type is kept unread (the keep after a read no longer
  re-makes the file from its content). Plan tests CreateFiresOnBirth, AsPathIsABirth green, red when reverted.
- **9b.list** — `variable.Use<TAs>` / `variable.Change<TAs>`; all 19 list actions one line; `Replace`
  answers a Data; `item.Where` (virtual) and `item.Holds` (the one predicate); `text.Split`,
  `number.Range`; new lists born through Create; `Ensure` takes a birth.
- **9b.llm** — a tool binds only its declared names (security); OnToolCall's and OnValidateResponse's state
  in a frame; the model's answer decoded one way (live and replay) through its kind, a birth.
- **9b.loop** — `action.list.After` is the loop body; the result dict born through its type.
- **9b.code** — `app.Code.Load(path, ctx, then)` is the one door a DLL comes in by (code.load, module.add);
  `app.Code.Register`; a missing DLL is the path's NotFound.
- **9b.channel** — the goal channel owns its defaults and direction.

## Open / waiting
- **Frames (Ingi):** a `Calls` frame keeps every write made under it. Does a frame scope only the names it
  binds? Until answered: foreach keeps save/restore; the callback frames stay.
- **Ordering over a missing field (Ingi):** `where %x% age > 20` over an element without `age` is an error
  (as a dict missing the key), not a skip.
- **llm cache** — left unfixed on purpose (Ingi: the full sweep's learning specimen).
- Logged in `Documentation/Runtime2/obp-cleanup.md`: list.Add/Flatten's extend fork, type.list.Full's flat
  copy, JSON read by hand in Tool.Arguments, a provider's DLL kept as a string.

## Next
9b continues one module per commit (candidates: error.throw, test.discover, then the worklist's others), 9c.
Cadence (Ingi's machine): builds and tests at `nice -n 19`; targeted suites each commit; plang tests each
module; the full C# sweep every third module or on a shared-layer change; `dotnet build-server shutdown`
between slices.

## Code example
```csharp
// an action is one line to the object that owns the work
public Task<data.@this> Start() => Path.Use(path => path.Read(Context, ResolveVariables));

// a list action: what the variable holds, as a list, handed on
public async Task<data<bool>> Start() => data<bool>.From(await ListName.Use(name => name.Use<list>(Context,
    async list => Context.Ok<bool>(await list.Contains(Value)))));
```
