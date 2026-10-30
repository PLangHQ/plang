# event-action — security re-run at 156afc9c5 (v2)
Verdict: **PASS** (F1, F2, F3 closed; F4 Low open, on the backlog "Sub apps and their makers").
- F1 (bf213173f): probe E green — the upload's `/system/Work` is now `Denied` like `/Work`.
- F2 (bf213173f): probe B2 green — the maker's check comes first; the sub's `system/event` on-error cannot take it.
- F3 (156afc9c5): B3 green (by F1), B4 green (check bound only on `/Wor*`; the sub's `/Own` binds `on error`; Work still `Denied`). The architect's question: yes — once the before side has answered, the denial is the guarded goal's; the sub's goal-level `on error` can only hide it from the sub's own caller, and the goal never ran (`worked` empty in A–D, B4).
- All earlier sets green (EditLaunder, Writable, InheritedBinding/Handler/Actor, IdentityCopy, IdentityMakeRace); stage 8 sets, Sub=none and actor-reach green. PlatformStory E skipped as F4.
