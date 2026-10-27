# v5 plan — the codeanalyzer review's fixes before the 7c landing (snapshot 123)

Rules: replace, never add beside; nothing new on a locked type (propose it); each commit green, reds by name, tests
per project; a bug's test is red before its fix. Each item is checked at HEAD first (some may be fixed already).

1. Bugs, in order: 10, 15, 21, 34, 9, 3, 13.
   - 10 needs a proposal for how a step's position is known, before the step list's twin loops change.
2. New on this branch: 4, 6, 8, 16, 17, 18, 19, 20, 26, 28, 32, 33, 35.
3. Older, scaled: 23, 24, 25, 27, 29, 30, 31, 36, 34's extras. A locked addition goes to the architect's list.
4. Bench: allocations per root birth and per request, by type and count, base d72538402 vs HEAD. Report only.
5. Item 2's trace: where Decode runs in View.Snapshot today, by caller.
6. Also before landing: run the plang suite on the wip (the review lists SavesTheRun and FreshAppGoesOn red).
7. Then tell the architect; codeanalyzer re-checks; then the landing (snapshot 122's order).
