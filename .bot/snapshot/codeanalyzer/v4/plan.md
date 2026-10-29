# v4 — recheck of fa88b00f8..c1e1a9248 (snapshot 344)
Delta: identity gates (SemaphoreSlim on default's make and the mutating verbs), auditor fixes (bound goal call carries every property, archive recheck, names), resume dedupe. Bench birth + request, PGO off.
Result: sub birth 99 KB, root birth 2353 KB, sub request 496 KB, root request 3814 KB, held flat (load 33, bytes unaffected). Verdict PASS.
Notes (low): Change() flows `_inside` through AsyncLocal, so a nested parallel start inside a change is unheld (documented in the comment); GetOrCreateDefault reads LoadAll on every ask (as before).
