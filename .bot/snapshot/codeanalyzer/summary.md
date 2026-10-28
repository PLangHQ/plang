# codeanalyzer summary — snapshot

**Version:** v1. Verdict: FAIL, coder next.

**What this is:** review and bench of 35c1b3377, where the snapshot is one write: the app writes itself in the snapshot view and reads itself back (ISnapshot, the node tree and the code records gone).

**What was done:** bench row (load 9+, bytes valid): sub birth 115 KB (+6, band 5), root birth and both request columns under runtime2's row, held memory flat, sweep 0 red, `plang --test` 307/0 after a rebuild. Read the 34 files; findings by pattern in `review.md`: P2-1 snapshot has a dead second case, P3-1/P3-2 own-type write is four type tests and a branch in the shared `View.Make`, P5-1 `Current` setter writes another field and `_resumed ?? _entered`, P7-1 two hand-called `Take`, P7-2/P9-1/P9-2 for Ingi's list. Not mutated.

**Example:**
```csharp
public call.@this? Current { get => _current.Value; private set => _resumed = value; }   // reads one field, writes another
internal call.@this? Resumed => (_resumed ?? _entered?.Stopped)?.Chain[^1];              // new ?? old
```
