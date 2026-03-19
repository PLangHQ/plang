# Tester v3 Summary — Deep Analysis After Coder v4

## What this is
Final deep-dive after all previous findings were fixed. Applied deletion test methodology to every code path, looking for what was missed.

## Test Run Results
- **C# tests**: 1647/1647 pass
- **PLang tests**: 0/10 — stubs

## New Finding: Auto-Create Overwrites User Data (major)

`GetOrCreateDefaultAsync` (types.cs:78-91) auto-creates an identity named "default" without checking if that name is already taken. The `Create` handler defaults `Name` to "default" via `[Default("default")]`.

**Scenario:**
1. User: `create identity` → Create(Name="default", SetAsDefault=false) — creates identity with keys K1
2. User: `get my identity` → Get(Name=null) → GetOrCreateDefaultAsync → no IsDefault found → creates NEW "default" with keys K2
3. SaveAsync overwrites K1 in DataSource — user's original key pair is destroyed

**Why I missed it before:** I was looking at each handler in isolation. This bug only appears when you trace the interaction between Create's default parameter value and the auto-create logic. The existing test `Get_NullName_NoDefaultExists_AutoCreates` uses names "a" and "b", never "default" — so it never triggers the collision.

**Fix options:**
- (a) GetOrCreateDefaultAsync checks if "default" name exists and promotes it to IsDefault=true
- (b) Use a different auto-create name (e.g., "default-1")
- (c) Error if name is taken

## Other Minor Findings
- No test for `Export(null)` when no default exists (404 path)
- PLang stubs still pending

## Verdict: needs-fixes
The auto-create overwrite is a data loss bug. Send back to coder.
