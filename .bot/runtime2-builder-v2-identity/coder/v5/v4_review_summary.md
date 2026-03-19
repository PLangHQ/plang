# v4 Review Summary (Tester v3)

1. **Major — Auto-create overwrites user data**: `GetOrCreateDefaultAsync` creates a new identity named "default" without checking if that name already exists. If user created one with `Create(Name='default', SetAsDefault=false)`, the auto-create silently overwrites their key pair.
2. **Minor — Export no-default path untested**: `Export(Name=null)` when no default exists (404 path) has no test.
3. **Minor — PLang stubs**: Still pending, blocked on builder prompt.
