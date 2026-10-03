Setting — the setting whose saved row to remove, whole: the `%!…setting%` variable as the step writes it (`%!llm.setting%`, `%!app.goal.list.setting%`). Always the whole setting node, never one field under it.

- `remove %!x.setting%` drops the actor's saved row so the setting goes back to the system's row, or its defaults.
- Clearing one value for this run — `set %!x.setting.field% = null` — is the variable module, never this.
