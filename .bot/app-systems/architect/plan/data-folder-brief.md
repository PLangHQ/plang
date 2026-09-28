# Parked brief: `.data/`, the app's state on disk, per owner and per user (after app-systems)

Ingi, 2026-09-28: "that .db folder should be named .data folder and in there is db, file, setting, etc.", then "settings/data.sqlite … we can put setting down to user, we could even have .data/%user.id%/setting/ (|file|cache|trace) so down to the user, but then default [is] what is above".

## The idea

```
.data/
  setting/data.sqlite        the app's defaults (today's system level)
  file/  cache/  trace/
  <user.id>/
    setting/data.sqlite      this user's own; a read falls back to .data/setting/
    file/  cache/  trace/
```

- **Each part owns its storage,** instead of one `.db/system.sqlite` holding every table (settings rows, setup's steps, the LLM cache, identities).
- **Per user, with the level above as the default.** It's the settings chain (user → system → defaults) made physical.
- **The tree on disk:** the plang path, the C# path, the file path and the storage path name the same thing (`%!app.setting%` ↔ `.data/setting/`). Prior art: Android's per-app `databases/`, `files/`, `shared_prefs/`, `cache/`.

## What it gives

- Isolation between users.
- Permission scoped to a user's folder (path authorisation already does in-root).
- The identity's private key living in its own user's folder.

## Open, for when it's picked up

1. **What is `%user.id%`?** The caller's identity (lean), which ties this to the parked "variable storage by identity". Today there's one `User` actor per app.
2. Is `cache/` per user or shared at the top (the LLM cache shared saves cost)?
3. Is a user folder made on first write only?
4. `trace/` moves out of `.build/traces/` (traces are data, not build output).
5. Setup's executed steps: whose are they (the app's)? Where does `store` go once each part owns its storage?

## Today (2026-09-28)

- `app/this.cs:605-618`: the one store, `/.db/system.sqlite` (in memory while testing).
- `BuildGoal/Start.goal:21`: traces saved under `/.build/traces/`.
