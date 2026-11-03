# set-add-order — coder summary

**Version:** v2

**v2 after review.** The codeanalyzer found that `Part` divided by the open modules' summed shares. When stage 1 answers
without probabilities, that is the module's own share, so `remove %x%` went Certain. `Part` now divides by what the
settled modules leave (1 − their shares), so the share no module claims stays doubt:
```csharp
var left = ranked.Where(m => settled.Contains(m.Module)).Aggregate((number)1, (rest, m) => rest - m.Share);
if (left <= (number)0) return 0;
return ranked.Single(m => m.Module == module).Share / left;
```
Boy scout: `Placed()` asks `parser.Read(at)` for each variable's place (it was an `IndexOf` scan), and `Assigned()`
reads a variable value through the parser; only the literal pattern stays a regex. New test:
`ARemoveAnsweredWithoutProbabilities_StaysPossible`. No pin moved.

**What this is.** `set %x% = %x% + 1` built `variable.set; math.add`: the set ran first and the sum was lost. The pick
list marked math.add only Possible because `Sure` read math's share of stage 1's one-of "main work" choice (0.67). A
two-module step always splits that choice, even when variable.set is already near-certain.

**What was done.**
- `PLang/app/goal/step/pick/list/this.cs`: `Part(module)` answers a module's share of the work no near-certain common
  action answers. `Certain(name)` (renamed from the local `Sure`) reads it. Nothing else changed: no new threshold, no
  prompt change, and the prompt still shows stage 1's shares as given.
- `PLang.Tests/Wire/App/Decider/ModulePartTests.cs`: (a) the set-of-a-sum, red without the change; (b) the `remove %x%`
  guard, green both ways.
- `pick_golden.json` re-pinned. Three picks moved Possible → Certain: checkout steps 3 and 5 (`if …, throw …`:
  error.throw, condition settled by condition.if) and loop step 2 (`foreach …, call Ship`: goal.call, loop settled by
  loop.foreach).
- Next: the builder's fresh-build measurement (set+add 5/5, `remove %x%`, the edge `remove %x%, write to %y%`, and
  doc/events.md's Mark).

**Code example.**
```csharp
private number Part(string module)
{
    var settled = Settled;
    var open = Ranked().Where(m => !settled.Contains(m.Module)).ToList();
    if (open.All(m => m.Module != module)) return 0.0;
    var sum = open.Select(m => m.Share).Aggregate((total, share) => total + share);
    if (sum <= (number)0.0) return 0.0;
    return open.Single(m => m.Module == module).Share / sum;
}
```
