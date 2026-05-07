# v6 Baseline (clean rebuild before any v6 change)

- C# (`dotnet run --project PLang.Tests`): **2757 / 2757 pass**, 0 fail.
- PLang (`cd Tests && plang --test`): **201 / 201 pass**, 0 fail, 0 stale.

After v6:
- C# **2760 / 2760 pass** (+3 — three new CallsTests for the new Set/Get model: `SetInsideOverlay_IsVisibleToSubsequentGet`, `SetInsideOverlay_DoesNotLeakToUnderlying`, `SetInsideOverlay_NewName_DoesNotEscape`, plus the converted `Push_ParallelFlows_EachSeesOwnBinding` and the new `SetInsideOverlay_DoesNotLeakToSiblingOverlay`).
- PLang **201 / 201 pass** (`Modules/Variable/Scoping/VariableScoping.test.goal` extended with the param-leak scenario; .pr rebuilt).
