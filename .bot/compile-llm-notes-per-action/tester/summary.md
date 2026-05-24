# Tester — compile-llm-notes-per-action — v1

## Version
v1 — fresh tester pass; no prior review.

## What this is
Test-designer authored 22 C# mechanism tests + 2 plang drift cases as failing-by-construction. Coder v1 made all the C# tests pass and shipped per-action markdown teaching across `os/system/modules/`. My job: verify on a clean build, then mutation-test the load-bearing rules so I'm not just trusting green.

## What was done

**Clean rebuild + full C# suite** (2942/2942 pass, 14s) on a freshly wiped bin/obj. All 22 new tests pass on first run with no flakiness. Targeted runs:
- `MarkdownTeachingLoaderTests` 6/6
- `MarkdownTeachingMergeTests` 4/4
- `MarkdownTeachingOrphanTests` 3/3
- `StepActionDetailsRenderTests` 6/6
- `CodeAttributeRegressionTests` 3/3

**Inspected committed `Tests/Simple/.build/start.pr`** — confirmed `output.write(Data=%message%)` with no `channel=` token, `assert.equals(Expected="hello plang", Actual=%message%)` with no `Message=` token. The drift the branch was built to close is closed in the committed pr.

**Mutation tests, all caught (4/4):**
1. Swap `MergeLayers` order (`m + a` → `a + m`) → MergeTests 1/4 fail.
2. Remove `ModuleStem` skip in `ScanOrphans` → OrphanTests 1/3 fail ("module.* is never an orphan").
3. Swap `notes`/`modNotes` in `Loaded` constructor → LoaderTests 3/6 fail.
4. Drop `planStep.actions` gate in renderer template → RenderTests 5/6 fail.
   `git diff --stat` clean after revert.

**Drift-case execution attempted.** Got blocked — see below.

## What's still in progress / next

Next bot: **coder v2**. One medium finding:

- **F1 (medium):** the two architect-spec'd plang drift cases (`Tests/Builder/CompileLlmNotes/{output-write-no-channel,assert-equals-no-message}.test.goal`) were committed **without `.pr` companions**. `plang --test` reports them as `[Stale]`, not Pass/Fail — the assertions never execute. The load-bearing safeguard for this whole branch silently no-ops today.

  Root cause is two-part. Building the `.test.goal` files requires `plang build`, which wedges before reaching `Tests/Builder/`:
  ```
  Building goal: Publish
    Planner validation failed: Planner returned 10 step plans but goal has 9 steps. — retrying...
    llm.query: Cannot convert ... to Boolean: Object must implement IConvertible.
  [exit 2]
  ```
  The inner "Cannot convert to Boolean" wedge in the planner-retry path is **pre-existing on parent** (fires on a different goal there). The outer trigger ("10 vs 9 step plans on `Publish.goal`") does **not** fire on parent — coder's prompt change shifted what the LLM emits on `Publish.goal` enough to trip the latent wedge earlier.

  **Coder v2 must:** ship `.pr` files for both drift cases (built or seeded) so `plang --test Builder/CompileLlmNotes` reports `2 pass, 0 fail, 0 stale`. Optionally unwedge `plang build` so the architect's 3-fresh-cache rule is mechanically reproducible.

## Decisions / open items for coder
- The 6 `StepActionDetailsRenderTests` cover the same drift mechanically (and are mutation-verified), so the drift IS prevented at the renderer layer. But the architect specifically asked for the 3× rebuild rule because single correct emissions don't refute LLM drift. The plang drift cases are the only thing that exercise that.
- The `plang build` wedge is two-step: the inner retry-path bug is out-of-scope shared infra, but the outer trigger on `Publish.goal` is shape-sensitive to this branch's prompts. A quick narrower-scope fix is fine; a deeper retry-path fix would be a separate PR.
- `dotnet test` is broken on the .NET 10 SDK ("VSTest target is no longer supported") — the TUnit binary `PLang.Tests/bin/Debug/net10.0/PLang.Tests` is the runner. Use `--treenode-filter "/*/PLang.Tests*/*ClassName*/*"` to scope.
