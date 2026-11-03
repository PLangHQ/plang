# set-add-order — auditor v1

Verdict: PASS. Next bot: docs. Audited 74bf815d1 (production d30db6c03, tests 37b2995fa) against runtime2 366c06e8c; security v2 8263522de read for the probes.

Pattern 12 (one answer): `Part(module)` is the one member that answers a module's certainty, read by the local `Certain`. No second accessor. Pattern 5 holds: shares are stage 1's own, nothing searched. Return early: `Part`, `Placed`, `Assigned` all guard first. Boy scout: `AssignsOne` and the IndexOf scan are gone, the parser reads `%...%`; nothing added to the parser (locked core untouched). Plan decisions 4/10, 9, 11, 13, 14 match the code.

Sweep (keys unset, rebuilt): Modules 1500, Types 1049, Wire 647, Data 882, Generator 191, Runtime 1513 green; `plang --test` 323 total / 322 pass / 0 fail / 0 stale / 1 skipped (runtime2's). Matches the tester. Not run: the live llm gate, mutations.

Findings
- A1 (minor, deferred) `Part` ends in `ranked.Single(...)`, which throws InvalidOperationException if the module is not ranked. Unreachable today (a Through pick comes from an asked, ranked module), but the plan's "0 when the module isn't open" guard was dropped in v2 with no replacement. A `SingleOrDefault` and return 0 would fail safe to Possible.
- A2 (note) F1 stays Low accepted risk (security v2, builder 21 runs, none in the window); the probes pin today's edge and flip when backlog item A lands. Agree.
