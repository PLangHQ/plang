# security — runtime2-callstack — v2 plan

## Subject under audit

Coder's response to v1's 5 findings — commits `d2d9d2be` (OBP refactor:
promote IError/Children lists to domain types) and `ec092e91`. Diff is
narrowly scoped to the four collections v1 flagged (`Audit`, `Errors.All`,
`Call.Children`, `Call.Diffs`) plus the `Call.Tag` write path.

## Approach

Re-audit only the changed surface against v1's findings. No re-scoping
of the broader callstack threat model — that was done in v1.

For each v1 finding, decide: **closed**, **partial**, or **open**.
Add new findings only if the refactor itself introduced new exposure
(domain class wrappers, back-reference to stack from Children).

## What I checked

1. **Finding 1 (medium — Audit/All race)**
   - `App/CallStack/Audit/this.cs` — domain class, private `_lock`,
     snapshot iteration. Add/Count/indexer all guarded.
   - `App/Errors/Trail/this.cs` — same pattern. `Errors.Push` now calls
     `Trail.Add(error)`, replacing the old `All.Add`.
   - `IReadOnlyList<IError>` interface preserved for PLang consumers
     (`%!callStack.Audit.Count%`, `%!error.Trail[0]%`).

2. **Finding 2 (low — Tag race)**
   - `Call/this.cs:156-163` — `lock(_tagsLock) { Tags ??= new(); Tags[k] = v; }`.
     Lazy alloc + write atomic.

3. **Finding 3 (low — public-list lock smell)**
   - Children: `Call/Children/this.cs` — domain class with private
     `_lock`. CallStack.Push and Call.DisposeAsync go through
     `Children.Add`/`Remove` — no public lock target.
   - Diffs: `Call/this.cs:31` introduces private `_diffsLock`, used at
     OnSet handler line 141. The collection itself is still
     `public List<Diff>?` — see new finding F2 below.

4. **Finding 4 (low — unbounded growth)** — unchanged. Audit and Trail
   still grow for App lifetime. v1 marked it as architect's by-design
   choice; coder didn't add bounds. Stays open at low.

5. **Finding 5 (low — stale `_root`)** — fixed at
   `CallStack/this.cs:106`: `if (caller == null) _root = call;` now
   reassigns on every top-level Push. Closed.

## New surface introduced by v2

- Domain classes for Audit/Trail/Errors/Children — each with private
  lock + snapshot iteration. Consistent pattern.
- `Children.@this` holds a back-ref to `App.CallStack.@this` so it can
  read `_stack.Flags.History`/`MaxFrames` to drive FIFO eviction. Adds
  one new race vector to verify (Flags torn read).

## Findings to write up

- F1 — Diffs reader race (asymmetry with the other four collections).
  Writes are now lock-protected on `_diffsLock` but the public field is
  still `List<Diff>?` — readers iterating the raw List can race with
  OnSet writes from sibling Task.WhenAll branches. Severity low,
  observability-only.
- F2 — `CallStack.Flags` torn read. `record struct` reassigned via
  public setter from `Debug.Apply`; `Children.Add` reads under its own
  lock but the underlying field is non-volatile. Worst case is one
  stale eviction decision. Soft concern, low.

## What I'm NOT going to do

- Re-audit threat model (already done in v1).
- Re-verify cycle detection, AsyncLocal scope, Restorer, CaptureBefore
  — none of those code paths changed.
- Re-flag pre-existing standing findings (FormatVerboseValue,
  AssertSnapshot) — those carry forward unchanged.
