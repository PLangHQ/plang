# Security Learnings — runtime2-settings v1

## Cross-Bot Collaboration: Commenting on Another Bot's Report

**Context:** Ingi asked the security bot to add comments to the auditor-report.json — not rewrite it, just annotate each finding with a security perspective.

**Pattern:** Add a `security_comment` field to each finding in the other bot's report JSON. Don't restructure or rewrite their findings. Place the comment inline next to the finding it responds to, so readers see both perspectives together.

**Why it matters:** The multi-bot workflow produces reports from different angles (auditor, tester, security, codeanalyzer). Cross-commenting lets bots build on each other's work without duplicating or contradicting. The auditor flags a contract bug; security adds whether it's exploitable. The tester flags a gap; security adds whether it's a regression risk for a security property.

**Rules:**
- Read the other bot's report first, understand what they're saying
- Add your field (`security_comment`, `auditor_comment`, etc.) — don't modify their fields
- Keep comments concise — one sentence on whether you agree, one on security implications
- Reference your own report if you already covered it (e.g., "Already flagged in security-report.json finding #3")
- If you disagree with severity, say so and explain why from your perspective

## Settings Infrastructure — Security Assessment

**Lesson:** Small, well-isolated features with trusted-only input paths produce mostly accepted-risk findings. The Settings system had 4 findings, none critical. The most important insight was forward-looking: "if this ever takes untrusted input, these accepted-risks become real vulnerabilities."

**Pattern:** When reviewing configuration/settings systems, always ask: "Can this value be set from an untrusted source, now or in the future?" If yes, validate bounds. If no (today), document the assumption so future changes don't silently break it.

## Clone() Semantics Matter for Security

**Lesson:** Reference vs. value copy in Clone() methods has security implications. The auditor caught that `PLangContext.Clone()` copies `SettingsScope` by reference — mutations in the clone affect the original. Today this is a contract bug. If contexts ever run with different trust levels, it becomes cross-trust-boundary pollution.

**Rule:** When reviewing any Clone/Copy method, check every reference-type field. Ask: "If the copy is mutated, does the original see it? Is that safe?"

## Catch Narrowing Can Regress — Enumerate All Throw Sites First

**Context:** Security recommended narrowing a bare `catch` in Cast<T> to only catch expected exceptions. Coder v3 did it. Tester v3 found a regression — `Enum.ToObject` throws `ArgumentException` for string values, which wasn't in the narrowed filter. The bare catch had accidentally been protecting against this.

**Lesson:** When narrowing a catch clause, enumerate every throw site in the try block and check what each can throw. Don't just list the "obvious" exceptions (`InvalidCastException`, `FormatException`, `OverflowException`). `Enum.ToObject` throwing `ArgumentException` is non-obvious but documented.

**Rule:** Before recommending catch narrowing, trace every method call in the try block and list their documented exceptions. The fix isn't "narrow the catch" — it's "make the catch match reality."
