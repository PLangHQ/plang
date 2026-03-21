# v1 Review Summary (from tester)

Tester found 13 issues, verdict: **needs-fixes**.

## Major (6)
1. `provider/list.cs` — 0% coverage, no test calls `list.Run()`
2. `provider/remove.cs` — UnknownType error path untested
3. `provider/setDefault.cs` — UnknownType error path untested
4. Ed25519Provider — all 3 catch blocks untested (invalid base64 inputs)
5. `SignedData.Verify()` — null-signature and invalid-base64 guards uncovered
6. `Providers/this.cs` ResolveType — only 'signing' branch tested, 4/7 untested

## Minor (7)
7. Sign_MissingIdentity — only checks `Success==false`, not `Error.Key`
8. Sign_ProviderThrows — only checks `Success==false`, not `Error.Key`
9. Verify_ProviderThrows — only checks `Success==false`, not `Error.Key`
10. `provider/load.cs` — 28% coverage (hard to test, needs real DLL)
11. `Providers/this.cs` — null-name guards on Remove/SetDefault untested
12. No PLang tests for provider module
13. `Settings.cs` — 0% coverage (POCO with defaults)
