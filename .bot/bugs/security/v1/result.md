# security — bugs, narrow check of ac584f4e1 (stores' folder standing)

Verdict: pass for the change; one Low residual (S1) that the old and new order share.

## Was the old order reachable from a step? No (latent).
Grants come only from `path.Keep` (exact, after an Ask) and a role's `Stand`. Standing denied a store place before any
ask, so Keep never saw one; the other Ask callers (build "create app", identity consent) keep nothing. Glob grants:
system = os + store; user = os read/execute only. A persisted row needs a write into `.data/setting` (a store, denied
to the user) or a shipped `.data` (ruling 317: it holds the key already). Links land first (Location), so a link into
`.data` is judged as a store place. Not probed: case-blind disk.

## The fix
`Stands` walks `_standing` only. System reaches its store (probe green); user does not (green, no ask).

## S1 (Low, both orders): an app rooted under the runtime's os folder
`os` = BaseDirectory/os; store = root/.data. If a root lies under `os`, its store is under `os`, where the user's role
stands read/execute. `Stands` finds that glob, so the user reads that app's key. Red on 3a29633f8 and on ac584f4e1:
`StoreStandingProbeTests.AnAppRootedUnderTheOsFolder_TheUserStillCannotReadTheStoresKey`. Reach: only an app run from
inside the runtime's own os folder. Fix shape (architect's): in the store branch, stand only on grants whose path is
the store's own (not the os glob), or test the store before the os grant.
