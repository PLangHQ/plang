# codeanalyzer summary (fast-apps)

**Version:** v6

**What this is:** The sixth fast-apps review: review5's fixes (decisions 88–89, tip 6420e4894) and the verdict for the branch. Six production files changed since review5. Read only; not run. Report: `.bot/fast-apps/codeanalyzer/review6.md`.

**What was done:** Every review5 finding is fixed at the root: `type.list.Add(kind)` is gone (the origin uses the one internal batch `Add`), the collision rule is one `Guard(IEnumerable)`, a batch is guarded before any kind is held and the origin holds a below-layer batch whole or not at all, setup asks by its fixed paths (no add-then-remove), the three call-frame pushes go through one depth door with static lambdas. Verdict **PASS** for the OBP review. Four small notes: `Holds`/`Hold` one letter apart, the scan's guard is looser than the old one (same owner and name), a private `Push<TState>` shares a name with the public pushes, `Held()`. One fact for security and Ingi, not an OBP finding: `dotnet list package --vulnerable` lists three High and two Moderate advisories on `SixLabors.ImageSharp` 2.1.13, against decision 89's "one moderate left".

**Next:** the tester's pass and security's full pass; Ingi on ImageSharp 3.x.

**Code example:**
```csharp
// one door for the depth check; the frame is made only if it passes     call/list/this.cs
private call.@this Push<TState>(TState state, Func<@this, TState, call.@this> make)
{
    if (_current.Value is { } caller && caller.Depth >= MaxDepth) throw new CallStackOverflowException(MaxDepth);
    return Enter(make(this, state));
}
```
