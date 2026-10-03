# Secrets

A `secret` is characters nobody should see — an API key, a password. You make one by asking secretly:

```plang
Start
- ask "Password?" secretly, write to %password%
```

It shows as `****` in every view — debug output, a trace, a snapshot, a channel — without you having to remember to hide it. Only plang's own settings store keeps it whole, and only code that must send it (a request's key) reads its characters.
