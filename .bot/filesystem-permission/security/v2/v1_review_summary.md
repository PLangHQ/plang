# v1 review summary — auditor's assessment of security v1

The auditor (`e23d32ac`, auditor-report.json) reviewed all four prior bots
including security v1. Their assessment of my work:

> **security: partial** — F1/F2/F4 well-analysed and severity-fair; **F3
> under-rated as non-blocking.** As a correctness defect with a false
> doc-comment and a false-greening test, F3 is branch-blocking, not "an
> improvement to land".

## What I got right
F1, F2, F4 — analysis and severity stood up. F3's *technical content* was
correct: I identified the 5-minute expiry, traced it to `signing.verify`
step 2's Created-age check, named the `Config.TimeoutMs` footgun, and wrote
the exact fix coder v6 ended up landing (`SkipFreshnessCheck`, not raising
`Config.TimeoutMs`).

## What I got wrong
**Severity framing of F3.** I rated F3 Medium and called all four findings
"improvements to land", verdict PASS. The auditor's point: a defect that
(a) makes the branch's headline feature not work as documented, (b) ships a
false doc-comment, and (c) is false-greened by a test that never advances
time — is branch-blocking when viewed as code+tests+docs together, even
though the *behaviour* is fail-closed (re-prompt, not bypass).

**Lesson:** "fail-closed, so not a security hole" is the right call for the
*security severity* — but a finding that doubles as a correctness defect with
a lying doc-comment and a false-green test should not be bundled under a PASS
verdict as a soft "nice to have". Either rate the verdict on the whole defect,
or hand it off explicitly as blocking-for-someone-else. Severity is
threat-model-relative; *blocking* is not only threat-model-relative.

## v2 response
F3 is now fixed (coder v6) and re-audited. This v2 verdict rates the branch on
the full current state: F3 closed, F1/F2/F4 open but precondition-gated, no
critical/high → PASS stands, this time without an under-rated finding hidden
inside it.
