# v1 review summary (coder response)

Coder addressed all three v1 findings across two commits:

- **064724fda** — F1 fix (HIGH, path-traversal). FilePath ctor now
  canonicalizes `_absolutePath` via `PathHelper.GetFullPath` before storing.
  Every code path (`Resolve`, derivation verbs, scheme registry, implicit
  `string→path`) inherits the fix for free. Adds the regression test I
  specified (`DotDotTraversalRegressionTests.cs`) covering the exact
  mutation shape from v1.
- **064724fda** — F3 cleanup, bundled with F1. Whole-file PLNG002 exemption
  for `PLang/app/this.cs` is dropped; `OsAbsolutePath` now routes
  through `PathHelper`. PLNG002 split into two narrow carve-outs:
  `Path.*` → only `PathHelper.cs`; `File/Directory/FileInfo/...` → only
  `app/types/path/**`. No remaining whole-file exemptions.
- **987a5148e** — F2 fix (LOW, latent-Medium). MarkdownTeaching's 8 ungated
  System.IO reaches lifted to the `path.@this` verb surface. Loader is
  now async; ripples through `IBuilder.Types`, `modules.Describe()`, and
  ~10 test call sites. Whole-file PLNG002 exemption dropped.

PathHelper (new) is a `System.IO.Path`-forwarder type. Contract: pure
name math, no IO. The only allowed bridge — PLNG002 carves it out for
`Path.*` only.

## What v2 verifies

1. **F1 fix is real** — read the diff, reason about the canonicalization
   coverage at every entry point (Resolve, JsonConverter, implicit
   operator, direct ctor), then mutation-test it.
2. **No new attack surface from PathHelper** — confirm the contract
   ("no IO, ever") holds in the body and that PLNG002's `Path.*`
   carve-out is the only place it can sit.
3. **F2 fix is real** — confirm every disk touch in MarkdownTeaching
   goes through `path` verbs and that `MarkdownTeachingRoot` overrides
   route through `Resolve` → AuthGate.
4. **PLNG002 narrowing is tight** — no whole-file exemptions remain;
   the two carve-outs are correctly scoped.
