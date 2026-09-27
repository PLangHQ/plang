# Slow tests (ActorDatasource 9 s, SettingsCrud 7.9 s): the architect's own trace, before the coder's

This is a static trace at 02a5d1f08. The binary was absent (the coder was rebuilding), so none of it is measured. The comparison goes at the end once the coder reports.

## What both tests do

Each test does `set %!x%` → `save %!x%` → `assert` → `remove %!x%`. These are the only tests that save or remove a setting row, so the time is in the settings store.

## The path

- `setting.save` → `actor.setting.Save` (`actor/setting/this.cs:119`).
  1. `owner.Load()` → `Read()` (`:197`). This is the **first touch of `app.store`** in the test's App: an in-memory SQLite store is created, then `GetAll` hydrates every row through the plang format's `Decode`, and each signed row is **verified** (`wire/kind/plang/this.cs:79-90`, an `App.Run(verify)`).
  2. `store.Set` (`store/sqlite/this.cs:167`) → `Format.Encode` → **sign-if-missing** (`wire/kind/plang/this.cs:38-47`, an `App.Run(sign)` with the store's system context).
  3. Signing needs the system's identity. With none saved, `identity/code/Default.cs:257-281` generates one (Ed25519, fast) and **saves it as a setting row**, which goes through `store.Set` → Encode → sign again, now with the new identity.
- `setting.remove` → `Remove` (`:139`) → `owner.Load()` again (held, so cheap) → `store.Remove`.

## Hypotheses, most likely first

1. **Identity bootstrap per test App.** Each test gets a fresh child App with a fresh in-memory store, so the first save creates the identity, saves it, and signs twice. It happens once per test, and only these two tests pay it. What to check: the time spent inside `GetOrCreateDefaultAsync`, and whether the identity save re-enters `actor.setting.Load` or sign.
2. **The cost of `App.Run` for sign and verify** (action construction, the setting lookups each action does, `%NowUtc%`), multiplied by the rows.
3. **Store construction** (SQLite in memory, `EnsureTable` on every call).

Ruled out:
- A channel or ask timeout: the channel's `Timeout` is 30 s (`channel/this.cs:54`), not 8.
- Key generation cost: Ed25519 (`signing/code/Ed25519.cs:163`).
- Any `Task.Delay` on the path: the only ones are `timer.sleep` and the `on.error` retry.

## Noticed on the way, for stage 12

- `store/sqlite/this.cs:130,160,190` wrap Get, GetAll and Set in `catch (Exception ex)`. A bug's exception then becomes a `SettingsError` result along with SQLite's own failures.
- `actor/setting/this.cs:205-210` catches `UnauthorizedAccessException or InvalidOperationException` around `app.store`. That catch goes away once the store opens itself (decision 129, stage 10).
