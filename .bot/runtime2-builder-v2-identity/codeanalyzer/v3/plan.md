# Code Analyzer v3 — Verify SaveAsync check fix

## Scope
- `types.cs:88-90` — SaveAsync result check + throw
- `get.cs:29-36` — try/catch converting to Data.FromError
- Trace: does the throw propagate correctly through IdentityData.ResolveDefault()?
