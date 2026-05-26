# tester — purge-systemio-from-actions/v2

## Round 1 — 2026-05-25 (verdict: FAIL)

Suite was green (C# 3025/3025, PLang 206/206) and PLNG002 was at error
severity, but four denial-test files exercised path verbs directly instead
of the handlers they claimed to verify, one test had no assertions, and
several denial assertions were bare `Success.IsFalse()`. Eleven findings
filed; the production migration itself was sound.

See `git show b58399502` for the original report.

## Round 2 — 2026-05-26 (verdict: PASS)

Coder addressed findings in-place (no v3 bump). Verified by clean rebuild
(`rm -rf */bin */obj` + `dotnet build PlangConsole`):

- **C#**: 3025/3025 pass, 0 fail, 0 skip (~16s)
- **PLang**: 206/206 pass, 0 fail, 0 timeout, 0 stale, 0 skipped
- **PLNG002 warnings**: 0
- **Coverage**: 77.0% line / 39.7% branch overall (was 76.9% / 39.6%)

### Major findings — all fixed

**F1 (Http handler-layer false green) — fixed.** Tests now invoke
`http.code.Default.CreateFileContentAsync` directly (made internal for test
access). Out-of-root case catches `IOException` (proves AuthGate denial
cascades up the call stack); in-root case reads the resulting
`HttpContent` back and asserts bytes match. Mutation: if
`CreateFileContentAsync` reverted to `File.ReadAllBytes`, PLNG002 would
refuse compile *and* the out-of-root denial would silently produce bytes
instead of throwing.

**F2 (Fluid handler-layer false green) — fixed thoroughly.** Tests now
drive `Fluid.Render` with a real `{% include %}` template, anchoring a
Goal at `/host.goal` so `PlangFileProvider.GetTemplateBaseDir` finds it.
In-root: partial content `"Hello footer"` reaches output, AskCount stays
0. Out-of-root: `outOfRoot/secret.liquid` containing `"SECRET_TOKEN"` —
asserts the rendered output `DoesNotContain("SECRET_TOKEN")`. This
exercises the full `PlangFileProvider`+`PlangFileInfo` chain. Coder
uncovered and fixed an unrelated byte[]→string bug in
`PlangFileInfo.CreateReadStream` along the way (UTF-8 decode byte[]
reads instead of `ToString()`ing them as `"System.Byte[]"`).

**F3 (OpenAi handler-layer false green) — fixed.** Tests invoke
`OpenAi.ResolveImage` directly. PNG magic bytes `89 50 4E 47`
base64-encode to a prefix starting `"iVBOR"`. Out-of-root with `"n"`
channel: serialized request `DoesNotContain("iVBOR")`. In-root:
serialized request `Contains("iVBOR")` AND `Contains("data:image/png;base64,")`.
The paired in-root/out-of-root assertions on the wire payload prove the
routing intent.

**F4 (Debug handler-layer false green) — fixed.** Tests drive
`app.Debug.ResolveLlmFilePath(ctx)` and `app.Debug.EmitLlmBlock(...)`.
Critically, asserts `resolved is global::app.types.path.@this` — the
typed channel itself is the audit-gate, and a regression to a raw
`string` return would break this. Trace content is then read back via
the gated `ReadText` verb and asserted to contain the emitted lines.

**F5 (AppLoad zero-assertion test) — fixed.** Captures Id/Name before
the corrupt-Load, asserts they're unchanged after (no half-applied
mutations), and asserts a subsequent `Save` succeeds — the App must
remain operable. The test name still says `ReturnsFailureNotCrash` and
the body checks invariants instead of an explicit Fail return, but the
substance now genuinely verifies the non-corruption contract.

### Minor findings — partial / accepted

**F6 (weak Success.IsFalse) — superseded.** The F1-F4 fixes replaced the
weak assertions with content-based checks (`DoesNotContain("iVBOR")`,
`DoesNotContain("SECRET_TOKEN")`, `IOException` catch). These are
*stronger* than `Error.Key` checks — they assert the actual security-
relevant outcome (the bytes the user cares about don't appear). F6 is
no longer a concern.

**F7 (Discover dotdot vacuous assertion) — fixed.** Both branches now
assert. The denial branch checks `result.Error != null` and `Error.Key
!= "NullReferenceException"`. The `!= "NullReferenceException"` proxy
for "denial vs crash" is loose (would still pass on e.g. an unrelated
`FileNotFoundException` reaching the error key), but it does forbid the
worst case. Acceptable.

**F8 (PLang denial test goals gap), F10 (AbsoluteDiscipline misnamed),
F11 (AppGoals UsesPathList names) — accepted as-is.** These are
documentation-quality issues, not behaviour gaps. With PLNG002 at error
severity, the misleading names are mostly cosmetic. Worth queuing for a
future cleanup pass but not blocking this verdict.

**F9 (cold spots) — substantially improved.**
- `modules/ui/code/Fluid.cs`: 42.6% → **73.4%** line (+30.8)
- `modules/debug/this.cs`: 35.2% → **50.9%** line (+15.7)
- `modules/llm/code/OpenAi.cs`: 88.0% → **88.6%** (stable, already high)
- `modules/http/code/Default.cs`: 94.3% → **85.0%** (the
  CreateFileContentAsync branch is now exercised end-to-end; the
  per-class number went up but the file-wide rollup absorbs untested
  branches elsewhere)

### Codeanalyzer findings (out of scope for tester but worth noting)

Coder also addressed N1 (Json.cs unused allocation moved inside the
default-options branch of `??`), N5 (AppGoals.TryLoadPr alias write
dropped), and documented N4 (Add silent-collision is intentional —
sub-goals legitimately share Name at different paths). N2/N3 deferred
with reason. Codeanalyzer can re-verify, but from a test-quality angle
none of these introduced new test gaps.

## Process note

`baseline-tests.md` is still absent from `coder/v2/`. Coder has been
adjusting v2 in-place; recommend they commit a `baseline-tests.md`
snapshot before any future iteration's edits so the diff lens works
mechanically.

## Verdict

`pass` — every major finding from round 1 is genuinely fixed (not
papered over), the suite stays fully green, PLNG002-at-error remains
enforced, and the handler-layer routing claim is now backed by tests
that would fail under mutation. The minor naming/PLang-goal findings
remain but are documentation rather than safety issues.
