# Tester — runtime2-builder-v2-crypto

**v1** — NEEDS-FIXES. 3 major: untested Engine.Providers API, false-green serialization test, algorithm override unverified. See [v1/summary.md](v1/summary.md).

**v2** — NEEDS-FIXES. v1 findings all resolved. Proper coverage now working (previous crash was wrong output path). Crypto source is 100% line coverage, but verify.cs has an unhandled `ArgumentNullException` path hidden by 100% line stats. Identity module has untested save-failure chains at 82-92%. See [v2/summary.md](v2/summary.md).

**v3** — NEEDS-FIXES. Coder v2 tests are correct but incomplete. v2 findings all resolved. New finding: 8 handler save/remove error paths (`create`, `setDefault`, `rename`, `archive`, `unarchive`) have zero test coverage. Same FailingSaveDataSource pattern makes these easy to add. See [v3/summary.md](v3/summary.md).
