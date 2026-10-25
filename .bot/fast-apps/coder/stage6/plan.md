# Stage 6 — a faster C# build (decision 64)

Project files, build settings and dev.sh only; past that it's its own branch.

1. **Measure first**, each with a binlog and MSBuild's performance summary:
   - clean: restore + build in a throwaway worktree (`/workspace/s6clean`), removed after;
   - no change: `dev.sh build` with nothing edited;
   - one PLang file touched: the inner loop's common case.
   Read where the time goes: restore, PLang's compile and its generator, each test project, the copy-local copies,
   the analyzers, and whether dev.sh restores every time.
2. **Remove unused packages** from `PLang/PLang.csproj`. The build is the proof: a namespace search can miss a
   transitive use. Then re-measure.
3. **Apply only the tricks whose win is measured** (e.g. reference assemblies / copy-local, shared output); each
   one measured outside the noise, or dropped.
4. Sweep, plang tests, commit per cause; the tester re-runs the suites.
