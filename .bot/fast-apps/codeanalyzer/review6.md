# fast-apps: sixth review — review5's fixes (decisions 88–89, tip 6420e4894) and the verdict

Scope: the six production files the coder changed since review5 (d050b9f50..6420e4894, +80/−100): `type/list/Registry.cs`, `goal/setup/this.cs`, `goal/list/this.cs`, `call/list/this.cs`, `module/code/this.Snapshot.cs`, `PLang.csproj`. Read from the diffs and the surrounding code (the setup candidates and `goal.IsSetup` whole). Read only; I did not run tests or `Tools/ObpScan`. I ran `dotnet list package --vulnerable --include-transitive` on `PLang` (it restores, nothing in the tree changed).

**Verdict: PASS for the OBP review.** Every review5 finding is fixed at the root and nothing new blocks by the branch's rules; the line can move on to the tester's pass and security. **One fact to take with it:** `SixLabors.ImageSharp` 2.1.13 still lists three High advisories when I run the scan, which contradicts decision 89's "one moderate left" (below). That is a dependency finding for security and Ingi, not an OBP blocker.

## review5's findings, one by one

| review5 | Verdict | Where |
|---|---|---|
| 1 (blocking) the public `Add(kind)` test-only | Fixed at the root | `type.list.Add(kind)` is gone; only `internal Add(IReadOnlyList<kind>)` remains, with its one production caller `origin/code.cs:18` `type.Add([…])`. A leftover single-kind call would bind to the base list's `Add(item)` and be refused at run time (`Admit`), so a missed site shows in the sweep |
| 2 the collision rule three times | Fixed | one `Guard(IEnumerable<kind>)` (`Registry.cs`); `Guard(kinds)`/`Formats()` gone; `Load` and `Add(assembly)` call `Guard(Held())`, the batch `Add` calls `Guard(Held().Concat(kinds))` only when one kind answers to a MIME or extension |
| 3a a later `Hold` refusal leaves the batch half held | Fixed | the batch guards first, then asks the origin to hold every kind of a type below this layer (`_origin.Hold(kind)`: the code holds every one, an actor none), then `Put`s its own: a refusal comes before any of the batch is held |
| 3b setup adds then removes | Fixed, justified | the `Remove` is gone. The candidates are the two fixed paths `/Setup.goal` and `/Setup/Setup.goal` (`goal/setup/this.cs:39-43`) and `goal.IsSetup` is `Name == "Setup" \|\| Where.StartsWith("setup/")` (`goal/this.cs:150`), so a goal read from either candidate is setup's by the path that names it; nothing read is taken back out |
| 4 the overflow throw in three overloads | Fixed | `call/list/this.cs` a private `Push<TState>(TState state, Func<@this, TState, call.@this> make)` checks `_current.Value is { } caller && caller.Depth >= MaxDepth` once, before the frame is made; the three deepening overloads pass static lambdas (no capture, no per-call delegate); `binding` and `Isolate` stay unchecked |
| 5 `held` local; unbraced `foreach` | Fixed | the stamp local is `path`; the snapshot's loop is braced under its guard |
| 6 ImageSharp 2.0.0 version | Fixed to 2.1.13 — **but see below** | `PLang.csproj` |

## Dependency check (verified, not an OBP finding)

`dotnet list package --vulnerable --include-transitive` on `PLang` at this tip:

```
> SixLabors.ImageSharp  2.1.13  2.1.13  Moderate  GHSA-gwg2-r3hj-4w44
                                          High      GHSA-j3p4-wp97-rph4
                                          High      GHSA-j9gm-c75j-xc9q
                                          Moderate  GHSA-wmxv-xphr-5c9g
                                          High      GHSA-jjfr-hcj7-qf5w
```
`Microsoft.Data.Sqlite` 9.0.21 and `Newtonsoft.Json` 13.0.4 list nothing. Decision 89 says that after 2.1.13 ImageSharp has "one moderate left (GHSA-gwg2-r3hj-4w44, fixed only in 3.x)". My scan shows that one plus three High and one more Moderate on the same version; the advisory feed may have moved since the coder's check, or the scans differ in what they list. Either way, "no high advisory left" does not hold against what NuGet reports today. The 3.x move was already put to Ingi; security should see this list, not the summary of it.

## New findings (none blocking)

1. **`Holds` / `Hold` are one letter apart** (`Registry.cs`): `Holds(kind)` is the fact "this kind's type is one of this list's own rows" (`ReferenceEquals(kind.Owner?.Types ?? this, this)`, the ruled comparison, 68), `Hold(kind)` puts it. Two near-identical names for different jobs; `OwnsTypeOf(kind)` would say it.
2. **The scan's guard is looser than before.** The old `Guard()` exempted only the same kind object (`ReferenceEquals(seen[key], kind)`). `Guard(IEnumerable)` exempts any kind with the same owner and name (`ReferenceEquals(taken.Owner, kind.Owner) && string.Equals(taken.Name, kind.Name)`), the batch's "a kind of the same name replaces that one". During the startup scan, two different kind objects of one owner and name from two assemblies now pass the guard and the second replaces the first at `Put`, where the old guard reported a clash. Probably the intent for a replace; say at `Guard` that the scan and the batch share it.
3. **A private `Push<TState>` shares its name with the public overloads** (`call/list/this.cs`) and its state travels as a value tuple; it works and allocates nothing, but a reader sees six `Push`es for two jobs (the public frame doors, the private depth door). `PushDeep` or `Deepen`, a noun-free name for what it does.
4. **`Held()`** (`Registry.cs`) is a participle for "every kind on this list's types" (the Held family, already on the follow-up list).

## Checked, clean (explicit no)
- No static added; no `caller == null` or `_parent` null forks; no flag read at many doors; no lock held across an await in the changed hunks.
- Locked core: `type.list.Add(kind)` removed (a narrowing of the public surface; the golden re-pinned naming 35 and 88, per the log); `Guard`, `Held`, `Holds`, `Put` are private/internal; nothing public added to `app`, `item`, `type`, `type.list`, `kind`, `property`, `reader`, `IWriter`, `data`, `call` or `event`.
- No history or task comments added.
- The setup behaviour is unchanged from review4's intent (only setup goals kept) without the add-then-remove.
- Bench: nothing in this round touches the numbers I recorded; review4's row and review5's bytes stand (root birth 2,360 KB, held memory flat). The tester's quiet clean-build row (33.8–53.8 s) is in, so stage 6 is not slower.

## For the coder
Cheap, optional: 1, 3, 4 are names; 2 is a comment at `Guard`. For Ingi/security: the ImageSharp list above.
