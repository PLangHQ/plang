# Auditor v1 — plan

## Task
First audit of `path-polymorphism`. The branch already cleared the per-bot
gauntlet — security v3 PASS, codeanalyzer v4 NEEDS WORK (low only),
tester v8 PASS — but no cross-cutting audit has run yet. My job: confirm
the pipeline's PASS holds up under independent verification, and look for
the things a single-axis reviewer would not see (architecture drift across
all 7 stages, doc-comment inversions across moved namespaces, cross-scheme
seams, latent userinfo / private-data leaks the per-axis bots wouldn't
chase).

## Pipeline state recap
- **architect v1** — 7-stage plan (namespace move → abstract Path → scheme
  registry → handler one-liners → [PathScheme] attr → HttpPath → contract
  base tests).
- **coder v1 → v10** — landed everything: namespace move, abstract base,
  per-App `Scheme` registry, FilePath/HttpPath, deleted IFile +
  DefaultFileProvider, typed Data<T> returns sweep, security fix cascade
  (S1 SSRF, S2 identity leak, S3 query-string hint, S4.a IDN homograph,
  S4.b userinfo strip), 307 body re-buffer.
- **codeanalyzer v4** — NEEDS WORK low: F1 (Data<T>.From silent value
  drop — doc-only), F2 (orphan summary block on DescribeReturnTypeName).
  Both addressed by coder v7 (commit d30f84c77) without a re-review.
- **security v3** — PASS. v9 closed S1/S2/S3; v10 closed S4.a/S4.b + O3.
  F1/F2/F4 carry-forwards unchanged at prior severities.
- **tester v8** — PASS. C# 2906/2906, plang 204/204. Mutation-verified
  N1–N4 + F4-CARRY.

## Audit focus (cross-cutting seams)
1. **Pipeline closure depth.** Trace each closed security finding (S1–S4
   + O3) and each codeanalyzer F1/F2 to the actual code, not the
   re-reviewer summary.
2. **7-stage delivery completeness.** For each architect stage: does the
   end state match the design? Namespace move complete? Scheme registry
   per-App + lock-free? PathScheme marker decorated and unconsumed-by-
   design? IFile / DefaultFileProvider gone? Cross-scheme CopyTo/MoveTo
   tested?
3. **Doc-comment drift across the namespace move.** Auditor v2 on
   `filesystem-permission` found an inverted permission-class doc-comment
   in actor/permission/this.cs that the F-A fix missed in the sibling
   filesystem/permission/this.cs. After the app.filesystem → app.types.path
   namespace move on this branch, did equivalent drift sneak in?
4. **Console.* and lowercase-namespace discipline.** CLAUDE.md bans
   `Console.*` writes in production. Lowercase namespace under `app/` is
   the rule (`app/filesystem/Default/` carve-out no longer exists post-
   move). Audit both.
5. **Latent userinfo / private-data leaks.** S4.b strips `_uri.UserInfo`
   for the gate/wire/persisted-key triple. Does *anything else* on
   HttpPath still carry the userinfo-bearing form (e.g. `Raw`, traces,
   error messages)?
6. **Independent build + full suite.** Don't trust reported counts —
   rebuild from clean, re-run both suites.

## Method
- Clean rebuild (`rm -rf */bin */obj && dotnet build PlangConsole && dotnet build PLang.Tests`)
- Run plang suite from `Tests/` (avoids stale .test.goal trap).
- Run TUnit suite directly via the built executable.
- Code-trace each finding closure (search by file/line, not by claim).
- Cross-cutting grep for: `^namespace [A-Z]` under `PLang/app/`,
  `Console\.` in `PLang/app/types/path/`, `\.Raw` usage scope.

## Deliverables
- `.bot/path-polymorphism/auditor/v1/{plan,result,verdict}.{md,json}`
- `.bot/path-polymorphism/auditor-report.json`
- `.bot/path-polymorphism/auditor/summary.md`
- `.bot/path-polymorphism/report.json` append
