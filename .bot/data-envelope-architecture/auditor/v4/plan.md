# Auditor v4 — Review Plan

## Scope

Review the complete data-envelope-architecture branch (Phases 1-4) as the coder's v4 is marked ready for auditor by the tester (v5 approved).

## What I'm Reviewing

1. **Phase 1**: `Engine.Types` — type knowledge consolidation
2. **Phase 2**: Type context + lazy derivation, Data context propagation
3. **Phase 3**: Data partial class split + Out view
4. **Phase 4**: Envelope pipeline (Wrap/Compress/Encrypt/Decrypt/Decompress/Unwrap)

## Files to Review

### Production code
- `PLang/Runtime2/Engine/Types/this.cs` — new Engine.Types class
- `PLang/Runtime2/Engine/Memory/Data.cs` — core Data with context, lazy Type
- `PLang/Runtime2/Engine/Memory/Data.Result.cs` — result/error concern
- `PLang/Runtime2/Engine/Memory/Data.Navigation.cs` — navigation concern
- `PLang/Runtime2/Engine/Memory/Data.Envelope.cs` — envelope pipeline
- `PLang/Runtime2/Engine/Memory/MemoryStack.cs` — context propagation
- `PLang/Runtime2/Engine/Context/PLangContext.cs` — context stamp
- `PLang/Runtime2/Engine/Goals/Goal/Steps/Step/Actions/Action/Methods.cs` — result context stamp
- `PLang/Runtime2/Engine/View.cs` — Out view enum
- `PLang/Runtime2/Engine/this.cs` — Engine.Types property

### Test code
- `PLang.Tests/Runtime2/Types/EngineTypesTests.cs` — 62+ tests
- `PLang.Tests/Runtime2/Memory/DataTests.cs` — 63+ tests
- `PLang.Tests/Runtime2/Memory/MemoryStackTests.cs` — context tests

## Review Checklist

- [ ] OBP compliance (5 rules)
- [ ] Thread safety (Engine.Types is mutable, shared)
- [ ] Error handling (Errors.Error vs ServiceError consistency)
- [ ] Contract fidelity (Type.Kind, Compressible navigation)
- [ ] Serialization round-trip correctness (RehydrateNestedData)
- [ ] Test adequacy (assertions strong enough?)
- [ ] Ripple impact (Data is foundation — any issue is global)
