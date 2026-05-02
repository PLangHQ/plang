# codeanalyzer — runtime2-callstack — v1

## What this is

Code-simplicity review of coder/v1: the callstack refactor (AsyncLocal Call tree,
Errors namespace, Cause linkage, Variables collection events, tag action,
--debug={callstack:...}). 7 commits across `PLang/App/CallStack/`,
`PLang/App/Errors/`, `PLang/App/this.cs`, `PLang/App/modules/{debug,error}/`,
`PLang/App/Variables/this.cs`, `PLang/App/Debug/this.cs`.

## What was done

5-pass review (OBP / Simplification / Readability / Behavioral / Deletion).
19 findings, 0 MAJOR, 5 MINOR-with-fix-recommendation, ~14 NITs.

The five MINORs to send back:

1. **`CallStackFlags.Tags` flag is documented but never checked** — `Call.Tag()`
   and `tag.cs` write unconditionally. Either gate writes on the flag or
   change the doc.
2. **`Errors._current` is `static readonly AsyncLocal`** on a per-instance
   class — directly contradicts the per-instance AsyncLocal CallStack
   adopted (CallStack/this.cs:22 comment) for test isolation.
3. **`stack.Push(...)` runs outside App.Run's try/catch** —
   `CallStackOverflowException` escapes as a CLR exception instead of
   becoming a `Data.FromError(ServiceError)` like every other failure.
4. **`Call.Diffs.Add` is not thread-safe** — fired from `Variables.OnSet`
   without a lock; parallel branches mutating the same Variables race on a
   `List<T>`. Children list got `lock(...)` for the same reason.
5. **`context.Step.Context = context`** in App.Run mutates a shared Step
   instance and isn't restored in finally. Parallel dispatch of the same
   Step (legal in `goal.call` under Task.WhenAll) overwrites another
   branch's pointer.

Plus a dead-code finding: `SerializableCallStack.cs` is defined and aliased
in test GlobalUsings but has zero references in production. Either wire it
into trace export (architect intent) or delete it.

## Code example — finding #10 (Tags flag is dead)

`CallStackFlags.cs:10` says:

```
- Tags: allocate Call.Tags dict on demand (no-op writes when off)
```

But `Call.Tag` (Call/this.cs:135-139):

```csharp
public void Tag(string key, string value)
{
    Tags ??= new Dictionary<string, string>();
    Tags[key] = value;
}
```

No flag check. Same in `tag.cs:30-43`. Flipping `--debug={callstack:{tags:false}}`
does nothing.

## Files written

- `.bot/runtime2-callstack/codeanalyzer/v1/plan.md`
- `.bot/runtime2-callstack/codeanalyzer/v1/result.md`
- `.bot/runtime2-callstack/codeanalyzer/v1/verdict.json` — `{ "status": "fail" }`
- `.bot/runtime2-callstack/codeanalyzer/summary.md` — this file's index entry

## Status / next

Verdict: **NEEDS WORK**. Send to **coder** for the 5 MINORs (any of the NITs
optional). After the round-trip, if clean, tester next.
