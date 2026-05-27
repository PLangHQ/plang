# Auditor v1 — findings

## Summary

Three reviewers approved. I found one **major** cross-file contract gap they all
missed: `HttpBuildHelpers.InferTypeFromUrl` does not gate stamps against the
PLang Types registry, so a literal URL with a recognised-but-unregistered
extension (.pdf, .html, .png, .docx, …) emits a `Type` stamp that runtime
`variable.set` then rejects with "Unknown type 'X'". `file.read.Build()` carries
this exact gate (lines 60–65); the sibling http path missed the lesson.

The other seams I checked are sound: Ask.ToString() consumers are migrated,
`ShouldExit` value-side opt-out wires through, and the Build() pass aborts
cleanly on `Fail`.

## Finding F1 — Major — cross-file

**File:** `PLang/app/modules/http/HttpBuildHelpers.cs:32-35`
**Missed by:** codeanalyzer (noted file.read's gate as N2 but didn't compare to sibling http handler), tester (no test exercises an unregistered extension), security (out of threat-model scope).

### What

`InferTypeFromUrl` filters out unknown extensions via `Formats.Mime(ext) ==
"application/octet-stream"`, but several MIME-mapped extensions are **not**
registered as PLang types:

| ext | `Formats.Mime` | `Types.Get` | result |
|---|---|---|---|
| `.json` | application/json | ✓ string-alias | stamps "json", works |
| `.csv`  | text/csv | ✓ string-alias | stamps "csv", works |
| `.html` | text/html | ✗ unregistered | stamps "html" → runtime Fail |
| `.pdf`  | application/pdf | ✗ unregistered | stamps "pdf" → runtime Fail |
| `.png`  | image/png | ✗ unregistered | stamps "png" → runtime Fail |
| `.docx` | application/vnd...wordprocessingml... | ✗ unregistered | stamps "docx" → runtime Fail |

After Build() stamps a value into the terminal `variable.set`'s `Type`
parameter (`builder/code/Default.cs:578`), runtime
`variable/set.cs:64-68` does `Types.Get(Type.Value)` and returns
`Fail("Unknown type 'X'", "UnknownType", 400)` if absent.

### Reproducer (build-clean, runtime-fail)

```plang
- get https://example.com/report.pdf, write to %report%
- get https://example.com/index.html, write to %page%
- get https://api.example.com/photo.png, write to %photo%
```

All three build cleanly (validation pass succeeds); execution fails with
"Unknown type 'pdf'" / "html" / "png". Pre-branch, the same goals worked
(Response.Body fell through to bytes or text).

### Why this is a real regression, not a theoretical one

- Literal URLs with non-text-shaped extensions are normal PLang inputs.
- The branch introduces the failure path — previously there was no Build()
  stamp at all, so variable.set received no `Type` and just stored the value.
- The compile LLM kernel rule (`Compile.llm`) was also tightened around `(type)`
  hints, so users have fewer escapes — explicit `write to %x%(object)` works
  but is an undocumented workaround for what should "just work".

### Why all three reviewers missed it

- **Codeanalyzer** observed the gate in `file.read.Build()` (its v3 note N2)
  but reviews file-by-file; HttpBuildHelpers sits in a different folder.
- **Tester** has `HttpRequest_Build_LiteralUrlWithExtension_InfersTypeFromExtension`
  but only tests `https://api/x.json` — a registered alias. The unregistered-
  extension case is not in the suite.
- **Security** doesn't audit for semantic regressions of this shape; semgrep
  rules don't fire.

### Suggested fix

Mirror the gate from `file/read.cs:60-65` in `HttpBuildHelpers.InferTypeFromUrl`
before the final `Ok(typeName)`:

```csharp
var typeName = ext.TrimStart('.').ToLowerInvariant();
if (app?.Types.Get(typeName) == null) return Task.FromResult(data.@this.Ok());
return Task.FromResult(data.@this.Ok(typeName));
```

Add a regression test alongside `HttpRequest_Build_LiteralUrlWithExtension_…`:

```csharp
[Test]
public async Task HttpRequest_Build_LiteralUrlWithUnregisteredExtension_ReturnsBareOk()
{
    var result = await Build("http", "request", ("Url", "https://x/report.pdf"));
    await Assert.That(result.Success).IsTrue();
    await Assert.That(result.Value).IsNull(); // not "pdf"
}
```

Mutation guard: deleting the new `Types.Get == null` line must flip this test red.

### Blast radius

Two action handlers (`http.request`, `http.upload`) share the helper, so the
fix is one location. The trailing variable.set in any goal that does
`get <literal-url-with-binary-extension>, write to %x%` is currently
broken. `http.download` is unaffected (writes to disk, unchanged signature).

## Other seams — clean

### Ask.ToString() migration is complete

Both named consumers (`path/this.Authorize.cs:69`, `path/file/this.Operations.cs:385`)
read `askResult.Value as Ask` and pull `.Answer?.Trim()` — they don't rely on
implicit `ToString()`. The ergonomic `Ask.ToString() => Answer ?? ""` is a
PLang-context affordance for `%name% equals "Alice"` and is documented inline.
No PLang test asserts the old `Type+`-shaped ToString output.

### ShouldExit wires the value-side opt-out

`data.ShouldExit` extension probes `Value is IExitsGoal` *before* the
`Type.Exit()` legacy branch. `Ask.ShouldExit() => Answer == null` correctly
gates the resume path. The flow through `path/Authorize.cs:65` and
`path/file/this.Operations.cs:382` is correct.

### Build() pass safety

`RunBuildPass` (`builder/code/Default.cs:537-556`) does **not** wrap each
Build() in a try/catch. `file.read.Build()` swallows its own broad exceptions
around the existence probe — that's an intentional best-effort warning.
`llm.query.Build()` and `HttpBuildHelpers.InferTypeFromUrl` are pure
synchronous reads of `__action.Parameters` with no IO and no string-format risk —
they won't throw under normal conditions. **Forward note (not a finding):** a
future Build() impl that does IO should follow file.read's swallow pattern;
otherwise validate aborts on the first IO hiccup. Worth surfacing in the
"good_to_know" doc for the typed-actions section.

### Channel("builder") silent-drop

Security F1 already flagged this as forward-risk. Confirmed: the only current
production caller in this branch is `file.read.Build()`. The pattern is
correct for build-time warnings (no consumer outside `plang build` ⇒ no-op
sink). It would be misused if applied to security-relevant signals; the
docstring on `Channels.Channel(name)` should make this explicit. Not a finding;
the forward-risk wording in security-report.json is sufficient.

## Previous reviewers' assessments

- **codeanalyzer v3:** **agree (partial)** — file-level work is thorough; the
  N2 observation about file.read's registered-types gate is the exact insight
  that would have flagged F1, but it stopped at the one file.
- **tester v2:** **agree (partial)** — mutation discipline is strong; suite
  is genuinely honest. Missed F1 because no test parameterises Build()
  across unregistered extensions.
- **security v1:** **agree** — F1 is not in security's threat model; their
  three opens stand.

## Verdict

**FAIL — one major cross-file contract gap.** Branch ships a regression for
the common `http.request → literal binary/HTML URL → write to %var%` pattern.
The fix is mechanical (~3 lines + 1 test) and the next bot should be coder.
