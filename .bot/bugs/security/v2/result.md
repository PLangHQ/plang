# security v2 — bugs full pass, tip 3c845fcf8 (parent runtime2 3a29633f8)

Verdict: PASS. No Critical/High open. Two Low findings (probes red by design), one informational.

## 1 Consent and the gate — clean
- StoreStandingProbeTests 3/3, PersistedGrantCannotOverrideStoresTests 7/7, GrantKeepTests 5, RoleStandingProbeTests 18,
  SubAppContainmentProbeTests 26: green. S1 (os-rooted store) closed by Place equality in `Stands`.
- N8 catch: only cancel / IOException / ObjectDisposedException / Unanswered AppException is "nobody could answer"; anything
  else comes back as its own Error. Every route out of Ask is a denial or an error, never a yes.
- N3: `Add` answers `Save`'s answer; `Keep` returns it. N4: `_standing` and `_once` are two lists; Add touches `_once` only,
  Revoke `_once` + persisted; Find reads standing, once, then persisted. `Stands` reads standing only, filtered by Place.
## 2 identity.export (N1) — clean
Refuses with ExportWaitsForCriticalActions (403) before any ask; IdentityPrivateKeyLeakTests 11/11 (the pin).
## 3 LLM answers file
- Key: headers are never kept; no committed `*.test.llm.json` holds a key/Bearer (grepped all six). `TheKey_IsNeverInTheFile` green.
- F1 (Low, test-only): the match is `data.Compare`, and text equality is OrdinalIgnoreCase, so a request differing only
  in case ("DELETE THE FILES" vs "Delete the files") is served the other's saved answer. Probe red:
  `LlmAnswersFileProbeTests.ARequestDifferingOnlyInCase_IsNotServedTheOtherAnswer` (the trailing-space case is green).
  Reach: a test using `test.setting.llm = saved`, nothing in production. Fix: compare the request's JSON text exactly.
  Also: a prompt that interpolates a secret is stored in the file in clear (the file is the test's data; informational).
## 4 context.Run — clean
`ActorRunMemoryProbeTests` (5, with a control that proves the probe sees a variable when the actor is the caller's own):
User→System and System→User, caller in a goal or not, read nothing of the caller's variables.
## 5 Channels born of their actor's — clean
`ChannelOwnershipProbeTests` (2): Register and Set of a System-born channel on the User's list throw; the User's list is
unchanged. Channels are only built with `ctx.Actor.Channel`; a step that names another actor runs in that actor's child context.
## 6 Fluid
- `| json`: `<`, `>`, `&` escaped inside the json (only ever in strings); `FluidJsonFilterProbeTests` 2/2. Only for a
  script context: `'` and `"` are not escaped, so it is not safe inside an HTML attribute.
- INFO (unchanged, off branch): plain `{{ x }}` renders with `NullEncoder` (Fluid.cs:187) while the comment above it says
  "HTML encoding for security (XSS prevention)". An untrusted value in an .html template is raw HTML. User-sovereign, so
  the author's call, but the comment is wrong and it wants Ingi's decision (default HtmlEncoder for html templates).
## 7 Screen frames
Bound runs before any decode in both forms (binary, text) and in the Windows window (it now calls Shown.Apply with the max).
`Fits` also caps at Array.MaxLength; negative w/h (u32 > int.Max) fail `width >= 0`. Text frames: malformed json/base64/types are
BadFrame. Closes the earlier Window.cs QOI `w*h` item (read only; Windows not run).
- F2 (Low): the bound is per rectangle, a message holds up to 65535. Each core rents `w*h*4` before the QOI is looked at,
  so a 40 KB frame of 2000 rectangles at 5000×5000 allocated ~1 GB (uninitialised pages, kept by the pool); at the 1 GiB the
  error message advises it is cores × 1 GiB. Probe red by design: `FrameAmplificationProbeTests`. Fix shape: a QOI of n bytes
  cannot make more than ~62n pixels, so refuse `w*h > 62*n + header` before renting (or read the QOI header first).
- Unchanged: `view`'s `_messages` BlockingCollection is unbounded (a slow Apply behind a fast sender grows without limit).
## 8 Command line — clean
Parse refuses a second goal, a setting path as the goal, and one name in two cases; Executor disposes the app on every
refusal; mode words take no value; `--x=false` plus its setting is refused. `%path%` is `app.filesystem.root` (landed).
StartRefusalTests 13/13. `Holds` fixes the sibling-prefix hole (`/root2` is not under `/root`) in Validate; ValidatePathTests 11/11.
## Not done
Case-blind disk and the Windows run (architect: with the Windows run). The json setting waits on Ingi.
