# fast-apps — coder summary

**Version:** the review line (decision 63 answered). Branch work is pushed; there are no `v<N>` folders on this
branch (the architect runs it by numbered decisions; `handoff.md` holds the detail).

## What this is

Making plang apps fast to start and cheap to make, sub apps above all (a request app per HTTP request, a test per
file). Each app's lists are a layer over what its origin answers (the code, or the actor that made it), and a birth
makes only what is asked for.

## What was done

- Stages 1–5 (see `handoff.md`, "Built, by decision"): the origin (`app.origin`), layered type/module lists, lazy
  births, class facts read once per code, the goal list's index and parallel discovery.
- Decision 63, codeanalyzer's review: every finding answered, one cause per commit (table in `handoff.md`). Held for
  Ingi: `module.Remove` answering `data` (11), the public default-true `autoWireConsoleChannels` (12).
- Decisions 75–83, the re-reviews (review2, review3): every finding fixed or ruled, one commit each; the hashes are
  in the replies to the architect. Among them, a data-safety fix (83): an app a testing app's actor makes is
  testing from its birth (`origin.Testing`), so no test run writes a real `.data`.
- Stage 6 (decision 64) is done: 56 unused packages removed, PLang on the plain SDK, test projects carry one
  runtime's native assets. A test bin ~270 → 30–42 MB, a checkout 3.3 → 1.5 GB, a no-change build 7.3 → ~4 s.
  `stage6/result.md` has the numbers; the clean-build bench row still needs a quiet machine.
- Next: codeanalyzer's review of the tip, and its bench row.

## Code example

What is made once and kept goes through one door:

```csharp
// before: copied eleven times, neighbours disagreeing
=> _permission ?? Interlocked.CompareExchange(ref _permission, new permission.@this(this), null) ?? _permission;
// after
=> System.Threading.LazyInitializer.EnsureInitialized(ref _permission, () => new(this));
```

The run-time bindings belong to the actor, not every context:

```csharp
var hold = Context.Actor!.on;   // was Context.Actor!.Context.on: a hold made per context
```

## After review

codeanalyzer flagged fork/stray-helper/stored-twice shapes and names (findings 1–18). The changes are in the
handoff table. Two divergences from the architect's rulings were said out loud and accepted (73): an actor's origin
answers Console = true (sub apps need output at boot), and Hold stays `error?` (no context on the load path).
