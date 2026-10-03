Name — the new identity's name, as the step names it (`create identity 'alice'` → Name="alice").
Default — true when the step says to make it the default (`set as default`, `as the default`). Left out otherwise.
Provider — the key provider, only when the step names one.

- identity.create makes a NEW identity. Reading an existing one is identity.get; switching which one is default (without creating) is identity.setDefault.
