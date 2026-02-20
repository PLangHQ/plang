# v1 Tester Review Summary

v1 found 1 critical, 3 major, 4 minor issues in the coder's Phase 1 (Engine.Types) tests.

**Critical:** Kind(null)/Mime(null) crash with ArgumentNullException — still present in v2.
**Major:** Name() false green (backtick for generic types), BuilderNames() and ComplexSchemas() untested — all still present in v2.
**Minor:** Compressible() unknown kind boundary unclear, PLang-specific type tests missing, nested generics untested, PLang tests deferred.

None of the v1 findings were addressed in coder v2 — the coder focused on Phase 2 (context + lazy derivation) rather than fixing Phase 1 test gaps. The v1 findings carry forward and compound.
