# v1 review summary

v1 ran without `git fetch` first. Security had completed locally but hadn't pushed.
v1's F1 ("no security review on branch") was a stale-state finding, not a real gap —
security existed.

Ingi: "each bot needs to run before [auditor]; if they aren't all done, then stop and
report." Captured as memory `feedback-upstream-bots-required` and as a character
proposal in `.bot/singular-namespaces/claude-md-proposals.md` (auditor v1 entry).

No other v1 finding changes. F2-F5 (latent codeanalyzer items, producer-stamping
invariant note) stand.
