# coder v4 — baseline tests

Captured before any edits, after a clean rebuild from `origin/data-normalize`
(commit `f1475688c`, tester v3).

## Rebuild

```
rm -rf PlangConsole/{bin,obj} PLang/{bin,obj} PLang.Tests/{bin,obj} PLang.Generators/{bin,obj}
dotnet build PlangConsole   # 0 errors, 455 warnings (pre-existing CS8604 nullability noise)
```

## C# suite

```
dotnet run --project PLang.Tests
→ total: 3381, succeeded: 3381, failed: 0, skipped: 0
```

## PLang suite

```
cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test
→ Test summary: 233 total, 233 pass, 0 fail, 0 timeout, 0 stale, 0 skipped
→ [Pass] /BuilderSanity/BuilderSanity.test.goal (261ms)
```

Both suites are fully green on origin before v4 edits.

## Note on tester v3's BuilderSanity claim

Tester v3 reported `/BuilderSanity/BuilderSanity.test.goal` as failing
(232/233). On a clean rebuild from the same origin commit it passes — the
checked-in `.pr` has `%items%` already parsed as a real `[1, 2, 3]` list,
so the string-atomicity rule in `PLang/app/data/this.cs:341` never enters
the picture for this fixture.

I cannot reproduce the failure. Most likely the tester rebuilt the fixture
locally and the non-deterministic builder produced a `.pr` where `%items%`
came out as a literal string, then `foreach` short-circuited. Their commit
didn't include the bad `.pr`, so the symptom didn't propagate.

v4 hardens the fixture against that class of non-determinism (see report).
