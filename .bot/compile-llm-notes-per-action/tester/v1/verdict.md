# Tester verdict — compile-llm-notes-per-action — v1

**Status:** NEEDS-FIXES (medium)

## What passed

- **Clean build:** 0 errors, 456 warnings (all pre-existing CS8604 noise on `Variable` implicit conversions and list-module generators — not from this branch).
- **Full C# suite:** 2942/2942 pass on a clean build (13.9 s).
- **All 22 new C# tests pass** on first run (no flakiness):
  - `MarkdownTeachingLoaderTests` 6/6
  - `MarkdownTeachingMergeTests` 4/4
  - `MarkdownTeachingOrphanTests` 3/3
  - `StepActionDetailsRenderTests` 6/6
  - `CodeAttributeRegressionTests` 3/3
- **Committed `Tests/Simple/.build/start.pr` shows the post-fix shape** — `output.write(Data=%message%)` with no `channel=` token, `assert.equals(Expected="hello plang", Actual=%message%)` with no `Message=` token. The drift the branch was built to close is, in fact, closed in the committed pr.
- **Mutation tests confirm the C# coverage is load-bearing (4/4 caught):**
  1. `MergeLayers` order swap (`m + a` → `a + m`) → MergeTests 1/4 fail (the order assertion).
  2. Remove the `ModuleStem` skip in `ScanOrphans` → OrphanTests 1/3 fail (the "module.* is never an orphan" assertion).
  3. Swap `notes`/`modNotes` in the `Loaded` constructor → LoaderTests 3/6 fail (both layer assertions plus a third).
  4. Drop the `planStep.actions` gate in the renderer template (iterate all actions instead) → RenderTests 5/6 fail (every block-omission and size-bound assertion).
  Source untouched in the final diff (`git diff --stat` clean).

## What failed / what's missing

### F1 — The two load-bearing plang drift cases never execute (medium)

Coder committed `Tests/Builder/CompileLlmNotes/{output-write-no-channel,assert-equals-no-message}.test.goal` **without their `.pr` companions**. `plang --test` reports them as `[Stale]`, not `[Pass]` / `[Fail]` — the assertions are never run.

```
Test summary: 206 total, 204 pass, 0 fail, 0 timeout, 2 stale, 0 skipped
  [Stale] Builder/CompileLlmNotes/output-write-no-channel.test.goal (0ms)
  [Stale] Builder/CompileLlmNotes/assert-equals-no-message.test.goal (0ms)
```

This is the architect's load-bearing safeguard — the reason the branch exists. Today it silently no-ops.

**Root cause is two-part.** Building the `.test.goal` files into `.pr` files requires `plang build`. That command wedges before it ever reaches `Tests/Builder/CompileLlmNotes/`:

```
Building goal: Publish
  Planner validation failed: Planner returned 10 step plans but goal has 9 steps. — retrying...
  llm.query: Cannot convert 'System.Collections.Generic.Dictionary`2[System.String,System.Object]'
             (Dictionary`2) to Boolean: Object must implement IConvertible.
        final:      Error: Cannot convert ... to Boolean: Object must implement IConvertible.
[exit 2]
```

- The **inner** error (`Cannot convert … to Boolean` on `llm.query.ContinuePreviousConversation`) is **pre-existing on parent** (`path-polymorphism`) — same trace fires there on a different goal. It is a wedge in the planner-retry path, not a regression from this branch.
- The **outer** trigger ("Planner returned 10 step plans but goal has 9 steps") on `Publish.goal` does **not** fire on parent — Publish builds cleanly there. So per-action notes shifted what the planner emits on `Publish.goal` enough to trip the retry path on this branch. The shape change is not necessarily wrong, but it surfaces the latent wedge earlier.

**What coder must do for v2:**
1. **Required:** ship `.pr` files alongside the two `.test.goal` files so `plang --test` actually executes the assertions. Either build them once the wedge is unblocked, or commit a hand-authored seed — the architect's 3-fresh-cache rule is meaningless until those `.pr` files exist.
2. **Recommended:** unwedge `plang build` end-to-end so the 3-fresh-cache rule is mechanically reproducible. If the retry-path bug is genuinely out-of-scope, file it as a follow-up and document the manual workaround at `.bot/.../tester/` so v2 reviewers can verify.

### Mitigations already in place
- The 6 `StepActionDetailsRenderTests` cover the same drift mechanically (the channel-routing rule is rendered only if `output.write` is in the planner's set; assert size bound < 16 KB on the system prompt body). These pass and are mutation-verified — so the drift is in fact prevented at the renderer layer.
- The committed `start.pr` is correct evidence that the round-trip works *once*.

But the architect specifically asked for the 3× rebuild rule because *single* correct emissions don't refute LLM drift. The plang drift cases are the only thing that exercise that. Today they don't.

## Other observations

- No regressions in the rest of `plang --test`: 204/206 pass, the 2 not-pass being the Stale drift cases. The other test trees are healthy.
- `Code` attribute regression (3 tests) confirms the `[Provider]` → `[Code]` rename held: type exists in `app.modules`, old name absent from non-`.bot` sources, PLNG001 diagnostic text mentions `[Code]`.
- The orphan scanner emits via warning channel (not `Console.*`) — checked against CLAUDE.md "No Console.* writes in production C#".

## What v2 should look like

Coder v2 closes F1 by shipping `.pr` files for both drift cases (built or seeded), so `plang --test Builder/CompileLlmNotes` reports `2 pass, 0 fail, 0 stale`. Optionally, document or fix the upstream `plang build` wedge so the architect's 3-fresh-cache rule is reproducible. With F1 closed, tester v2 verdict should be PASS.
