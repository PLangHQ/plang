# Code Analysis v1 — Plan

## Scope

Crypto module + Engine.Providers + identity-related changes (SensitivePropertyFilter, Actor.Identity, IdentityData).

### Files to analyze (crypto — new)
- `PLang/Runtime2/modules/crypto/hash.cs`
- `PLang/Runtime2/modules/crypto/verify.cs`
- `PLang/Runtime2/modules/crypto/types.cs`
- `PLang/Runtime2/modules/crypto/providers/ICryptoProvider.cs`
- `PLang/Runtime2/modules/crypto/providers/DefaultProvider.cs`
- `PLang/Runtime2/Engine/Providers/this.cs`

### Files to analyze (identity — from merged branch)
- `PLang/Runtime2/Engine/Context/Actor.cs` (Identity property, DynamicData registration)
- `PLang/Runtime2/Engine/Channels/Serializers/SensitivePropertyFilter.cs`
- `PLang/Runtime2/modules/identity/types.cs` (IdentityVariable)
- `PLang/Runtime2/modules/identity/IdentityData.cs`

### Files to verify (wiring)
- `PLang/Runtime2/Engine/this.cs` (Providers property)
- `PLang/Runtime2/GlobalUsings.cs` (EngineProviders alias)
- `PLang/Runtime2/Engine/View.cs` (SensitiveAttribute)
- `PLang/Runtime2/Engine/Channels/Serializers/Serializer/JsonStreamSerializer.cs`
- `PLang/Runtime2/Engine/Memory/Data.Envelope.cs`

### Test files (review for coverage gaps)
- `PLang.Tests/Runtime2/Modules/crypto/HashActionTests.cs`
- `PLang.Tests/Runtime2/Modules/crypto/DefaultProviderTests.cs`
- `PLang.Tests/Runtime2/Modules/crypto/ProviderResolutionTests.cs`

## Analysis passes
1. OBP Compliance — all 5 rules
2. Simplification — dead abstractions, redundant logic, premature generalization
3. Readability — naming, flow, consistency
4. Behavioral Reasoning — trace data origins, algorithm validation paths, generic catches
5. Deletion Test — "if I deleted lines X-Y, would any test fail?"

## Pre-read docs
- plang_object_based_pattern.md — DONE
- README.md — DONE
- good_to_know.md — DONE
- modules.md — DONE
