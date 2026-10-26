# decider-key — final audit (auditor)

**Verdict: PASS.** Tip 512445b0e (parent runtime2 493b157df). Next: docs. Nothing blocks the merge.

Checked at the tip with OPENAI_API_KEY and TYPESAFE_API_KEY unset: `./dev.sh build` clean. Sweep: Modules 1,424, Types 1,000,
Data 879, Generator 191, Runtime 1,390 green; Wire 612 with 6 red, the baseline's. `plang --test` (llm included, no key):
308 total, 303 pass, 1 fail (BareLiteralJudgedByForm, baseline), 3 stale, 1 skipped; tree clean. The live llm gate I did not
run (needs the key); the tester ran it (6/6 live, no file changed).

## Reviews assessed
codeanalyzer (FAIL v1 on the unpinned cause line, PASS v2): agrees; `IKey.cs:33` is now pinned in both no-one-to-ask tests
(`ChannelEof` in `MissingKeyTests` and `MissingDeciderKeyTests:85`). tester (PASS, four mutations): agrees. security (PASS;
`DeciderKeyProbeTests`, no report folder): agrees, I read the probe: the key lands on the asker's row, not the system actor's, is
masked in the Debug dump, not echoed by a 401.

## Read whole
`llm/setting/IKey.cs`, `llm/setting/this.cs`, `llm/decider.setting.cs`, `code/TypeSafe.cs`, `code/Live.cs`, and the tests
(`MissingDeciderKeyTests`, the `MissingKeyTests`/`DeciderProviderTests` diffs, `DeciderKeyProbeTests`). No older smell found.
The asking is written once (IKey's default `Asked`), each setting states only `Question` and `Missing`; the old `Asked` on llm's
setting is gone and no caller of it remains (`git grep '\.Asked('`: Live, TypeSafe). The key stays a Data to the header
(`Live.cs:21-24`, `TypeSafe.cs:31-42`); Live's dead empty-key guard is gone, correctly, since `Asked` succeeds only with a
non-empty key. TypeSafe returns before asking when every step is cached (`:25-26`), so a cached build still needs no key. The
ask's error rides whole in `Missing.list`, not glued into the message (the error rule). No locked surface moved (no golden diff).
Rule 35: nothing test-only.

## Findings
| id | sev | where | issue | disposition |
|---|---|---|---|---|
| D1 | nit (docs) | `docs/modules/llm.md:224` | still lists `llm.apiKey`; no doc names `TYPESAFE_API_KEY`, `%!llm.decider.setting.key%` or `TYPESAFE_ENDPOINT` | docs |
| N1 | nit, untested | `IKey.cs:20-39` | two concurrent `Asked` with no key held would each prompt (same for llm's key before this branch). A parallel build with no key is the case; I did not reproduce | next branch |
| N2 | nit | `IKey.cs:38` | `(setting.@this)this` is a cast an interface can't state; accepted by the codeanalyzer and the architect (2). Any non-setting implementer fails at runtime | none |
