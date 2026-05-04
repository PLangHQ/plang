# auditor — runtime2-callstack — v1 — PASS

## What this is

First and only auditor pass on `runtime2-callstack`. Three reviewers had already
run before me: codeanalyzer through v3 (PASS), tester through v1 (PASS after
stale-binary correction), security through v2 (PASS — closed everything except
F1 polish recommendation + F2 accept-and-document). After security v2, coder
shipped commit `5157d10a` ("close OBP asymmetry on Diffs/Tags/Flags + Flags.Parse")
addressing both. My job was to audit *that* commit through a cross-cutting lens —
not redo the file-level work the others did.

## Verdict

**PASS.** Branch is mergeable. Two nits, no critical/major.

- **F1 closed at the source, not just at the lock target.** Diffs and Tags are
  now domain classes (`App.CallStack.Call.Diffs.@this`, `App.CallStack.Call.Tags.@this`)
  symmetric with `Audit`/`Trail`/`Errors`/`Children`: private `List<T>` + private
  `object _lock` + `IReadOnlyList<T>` surface + snapshot enumeration. No raw
  `List<Diff>?` exposed publicly anymore — security v2's recommendation
  ("~30-line domain-class mirror") is exactly what landed.
- **F2 documented at the right place.** `CallStack/this.cs:30-35` carries the
  "record struct non-atomic copy under Apply" comment. Worst case stays at
  one off-by-one FIFO eviction, no data loss.
- **Flags rename is clean across the whole repo.** Zero residual
  `CallStackFlags` references in PLang, PLang.Tests, or Documentation.
- **Always-allocate Tags has no broken consumer.** No code anywhere null-checks
  Tags. The lazy-alloc → always-alloc swap is invisible to callers.
- **Build + tests on clean state.** 2623/2623 C# (TUnit), 181/181 PLang after
  rebuilding PlangConsole from scratch. The pre-rebuild 5-fail run reproduces
  tester's stale-binary diagnostic exactly.

## What I checked that the others didn't

- **Cross-file ripple of always-allocate Tags** — codeanalyzer v3 verified the
  diff itself was clean; I verified no consumer outside the diff still
  null-checks `call.Tags`.
- **DictionaryNavigator interaction with the new Tags type** — the doc claims
  `%!callStack.Caller.Tags.foo%` works via `DictionaryNavigator`. Traced through
  the three arms: Tags doesn't match arm 1 (IDictionary<string, object?>) or
  arm 2 (non-generic IDictionary), but matches arm 3 (`IDictionary<string, T>`
  via `GetStringKeyedDictType`). End-to-end PLang tests
  (`Tests/App/CallStack/TagBareLabelWritesTrue.test.goal`, `TagWritesPairsOntoCurrentCall.test.goal`)
  pass on clean rebuild.
- **F2 documentation actually exists** — security v2 said "accept and document".
  Verified the comment is on the property, not buried somewhere unfindable.
- **Run the tests on a clean PlangConsole** — don't trust either coder's or
  tester's count without re-establishing the binary state. PlangConsole binary
  was 3 days old; rebuilt and reran. 181/181.

## Findings (both nits)

### #1 — Tags Liskov compromise (architectural, doc'd)

`Tags` implements `IDictionary<string, string>` for `DictionaryNavigator`
recognition but throws `NotSupportedException` from `Add/Remove/Clear` via
explicit interface methods. Natural use (`tags.Set`, `tags["k"] = v`) is fine;
a consumer that upcasts to `IDictionary<string,string>` and calls `Add` gets a
runtime throw. Documented on the type, no internal caller trips it. Acceptable
as-is — flag for awareness only.

### #2 — Stale lazy-alloc comment (review-gap, cosmetic)

`Tests/App/CallStack/TagWritesPairsOntoCurrentCall.test.goal:2` reads
"Tags dict is lazy-allocated." That stopped being true at `5157d10a` (one dict
per Call now). Test still passes; comment misleads future readers. Tester
missed it. Coder should sweep the comment in a follow-up — or roll into the
next PLang docs pass.

## Code example — what F1 closure actually looks like

Before (security v2 state, `Call/this.cs`):
```csharp
public List<Diff>? Diffs { get; }              // raw List exposed
private readonly object _diffsLock = new();    // writer locks, readers race
```

After (this branch, `Call/this.cs:95` + `Call/Diffs/this.cs`):
```csharp
public Diffs.@this? Diffs { get; }             // domain type, no raw list
// Diffs/this.cs:
public sealed class @this : IReadOnlyList<Diff>
{
    private readonly List<Diff> _entries = new();
    private readonly object _lock = new();
    public void Add(Diff diff) { lock (_lock) _entries.Add(diff); }
    public IEnumerator<Diff> GetEnumerator()
    {
        Diff[] snapshot;
        lock (_lock) snapshot = _entries.ToArray();   // readers safe under writer
        return ((IEnumerable<Diff>)snapshot).GetEnumerator();
    }
    // ...
}
```

Mirrors `Children`, `Audit`, `Trail`, `Errors`. The five+one collection set
finally shares one shape.

## Files written

- `.bot/runtime2-callstack/auditor/v1/plan.md`
- `.bot/runtime2-callstack/auditor/v1/summary.md`
- `.bot/runtime2-callstack/auditor/v1/verdict.json`
- `.bot/runtime2-callstack/auditor-report.json`

## Next bot

PASS → suggest running the **docs** bot to apply CLAUDE.md proposals and merge.
