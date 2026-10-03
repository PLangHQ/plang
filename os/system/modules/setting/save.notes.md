Setting — the setting to save, whole: the `%!…setting%` variable as the step writes it (`%!llm.setting%`, `%!app.goal.list.setting%`). Always the whole setting node, never one field under it.

- `save %!x.setting%` saves the whole row so the runs after this one read what it holds now. Changing one value for this run only — `set %!x.setting.field% = v` — is the variable module, never this.
- `save %x% to file '…'` writes a file: that is file.save. This takes only a `%!…setting%` node, never a file path.
