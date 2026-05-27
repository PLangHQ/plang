# v1 review summary

Auditor v1 returned **FAIL** with one major cross-file finding:

**F1 (major, cross-file)** — `HttpBuildHelpers.InferTypeFromUrl` was missing
the registered-types gate that `file/read.cs:60-65` carries. Literal URLs
ending in MIME-known but PLang-unregistered extensions (`.pdf`, `.html`,
`.png`, `.docx`, ...) got stamped as the variable.set `Type` at validate time;
runtime `variable.set` (line 64-68) then rejected with `"Unknown type 'X'"`.

Reviewers missed: codeanalyzer v3 noted the gate on file.read as N2 without
comparing to the sibling http helper; tester only covered `.json` (a
registered alias).

Suggested fix in v1/result.md was ~3 lines + 1 regression test. Coder v2
(`8576f2dc6`) implemented exactly that.
