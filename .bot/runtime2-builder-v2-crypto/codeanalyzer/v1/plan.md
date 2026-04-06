# Code Analysis v1 — Plan

## Scope

Crypto module + Engine.Providers + identity-related changes (SensitivePropertyFilter, Actor.Identity, IdentityData).

### Files to analyze (crypto — new)
- `PLang/App/modules/crypto/hash.cs`
- `PLang/App/modules/crypto/verify.cs`
- `PLang/App/modules/crypto/types.cs`
- `PLang/App/modules/crypto/providers/ICryptoProvider.cs`
- `PLang/App/modules/crypto/providers/DefaultProvider.cs`
- `PLang/App/Engine/Providers/this.cs`

### Files to analyze (identity — from merged branch)
- `PLang/App/Engine/Context/Actor.cs` (Identity property, DynamicData registration)
- `PLang/App/Engine/Channels/Serializers/SensitivePropertyFilter.cs`
- `PLang/App/modules/identity/types.cs` (IdentityVariable)
- `PLang/App/modules/identity/IdentityData.cs`

### Files to verify (wiring)
- `PLang/App/Engine/this.cs` (Providers property)
- `PLang/App/GlobalUsings.cs` (EngineProviders alias)
- `PLang/App/Engine/View.cs` (SensitiveAttribute)
- `PLang/App/Engine/Channels/Serializers/Serializer/JsonStreamSerializer.cs`
- `PLang/App/Engine/Memory/Data.Envelope.cs`

### Test files (review for coverage gaps)
- `PLang.Tests/App/Modules/crypto/HashActionTests.cs`
- `PLang.Tests/App/Modules/crypto/DefaultProviderTests.cs`
- `PLang.Tests/App/Modules/crypto/ProviderResolutionTests.cs`

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
