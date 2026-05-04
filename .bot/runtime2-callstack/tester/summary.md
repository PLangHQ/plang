# tester — runtime2-callstack

## v1 — 2026-05-04 — pass (corrected)

Initial verdict was `fail` (5 PLang failures); corrected to `pass`
after env-divergence root-caused. The 5 failures were a stale
`PlangConsole/bin/.../plang` binary inherited across sessions —
pre-dating `be77dc12`'s `app.modules.debug.tag` and `%!callStack.Audit`
wiring. After `rm -rf bin/obj && dotnet build PlangConsole`, suite is
181/181 on the same commit. Coder/v2 was honest. C# 2623/2623 throughout.
Five test-quality findings (4-8) still stand as minor cleanups. See
[v1/correction.md](v1/correction.md).
