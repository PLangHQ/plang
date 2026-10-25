# packages: fourth review (ce2e14bb0 screen.setting.max, 0a3d1f030 SurfaceTests.Diff; b8215a0b9, 42c04d10e read in passing)

Read the diffs and `module/archive/setting/this.cs` (the shape copied). Rebuilt; `PngTests` 5/5, `SurfaceTests` 2/2 (3/3 with the coder's new one after the pull), `plang --test` (llm excluded, no key) 292 pass, 1 fail (the parent's), 3 stale, 7 skipped. No runtime-path or build file changed, so no other bench part.

**Verdict: PASS.**

- `module/screen/setting/this.cs` is archive's setting shape (`Internal`, `[Out, Store, Default("128 MiB")] size Max`): a module-owned setting at the edge, not the locked core; the one golden line (`type app.module.screen.setting`) is pinned with the decision.
- `Bitmap(encoded, max)`: `(long)w*h > max.Value/4` before any decode, no overflow, the sides never multiplied as ints; the BadFrame names the flag and how to raise it; `Bytes` is read inside the guard. The earlier screen-size bound is gone, so a frame larger than its window is shown again (the rule the architect and security settled).
- `SurfaceTests.Diff`: the clamp at the producer of the counts, and a test that was red (`Repeat` with a negative count).

## Notes (not blocking)
- `window/this.cs` `Draw` reads `screen.setting.max` on every draw (:117), including the high-frequency `{"rect…}` patches that never decode; only the frame line and the binary frame need it. Move the read into those two branches.
- `volatile size _max = null!` is a mirror of a setting, set by `Draw` and read by the decode thread. It is set before any frame reaches `Offer`, so never null where read; it exists because `Window.Offer` keeps the first decoder it is given. Passing the max with each offered frame would leave no mirror. Small; a note for the Windows follow-up.
- `PackagesPassProbeTests` (security's probes, kept as tests) is named for a pass; its 8000 × 8000 case asserts a peak working set, which a loaded machine can move.

CHECK: PASS. The tester and security are next.
