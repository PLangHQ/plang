# Cross-Cutting Concerns

These concerns apply throughout all phases of the migration.

## Strong Typing Discipline
- Every new module must declare explicit parameter types (never `object` without justification)
- Data.Type must be set correctly on all return values
- TypeMapping must be extended for any new types
- Automatic type coercion where possible, error where not

## OBP Compliance
- Collections own their operations (no external iteration)
- Navigate through object graph, don't decompose
- Per-request state as parameters, per-object state as properties

## Source Generator Compatibility
- Every handler must be `partial class` with `[Action]` attribute
- Parameter records must have virtual properties for source generator
- Test mocks must implement `ICodeGenerated` manually

## Error Pattern Consistency
- All modules return Data.Ok() or Data.FromError() - never throw exceptions
- PLang dev errors (validation) vs C# runtime errors (unexpected) are distinct
- Step-level OnErrorGoal catches failures
- Error display via `/system/error/` PLang goals
- Retry logic wraps Step execution

## PLang-First Philosophy
- If it can be written in PLang, write it in PLang (not C#)
- C# only for underlying tech primitives
- Overrides via goal calls, not DLL injection
- `/system/` ships with runtime, `/app/` is user space

## Identity & Signing
- Multi-standard: ed25519 (default) + web standards
- Core-level support (see `SigningService/PlangSigningService.cs/VerifySignature()`)
- `%Identity%` available everywhere, no separate auth module

## Culture & i18n
- CultureInfo support for formatting
- TString type for translatable strings
- Consider culture in all user-facing output
