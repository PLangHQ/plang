# Tester — runtime2-builder-v2-crypto

**v1** — NEEDS-FIXES. 3 major: untested Engine.Providers API, false-green serialization test, algorithm override unverified. See [v1/summary.md](v1/summary.md).

**v2** — NEEDS-FIXES. v1 findings all resolved. Fresh-eyes found 1 major: Verify.Run() throws unhandled ArgumentNullException when Hash is null (catch only handles FormatException). 3 minor edge cases. See [v2/summary.md](v2/summary.md).
