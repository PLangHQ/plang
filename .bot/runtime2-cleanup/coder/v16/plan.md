# Stage 16 — coder plan (`static-state-eviction-sweep`)

Rule C — static fields are a missing `@this`. Move where mechanical;
defer where the static-caller chain forces cascading invasive change.

## Migrations done

| Site | Action |
|------|--------|
| `Data/this.Envelope.cs` `_envelopeJsonOptions` | static → instance ✅ |
| `Channels/Serializers/Serializer/Plang/Data.cs` `_options` | static → instance ✅ |
| `modules/builder/providers/DefaultBuilderProvider.cs` `_buildTimer` | static → instance ✅ |
| `modules/llm/providers/OpenAiProvider.cs` `_requestCount` + cap const + increment-and-throw block | **DELETED** per Ingi 2026-05-07; todos.md entry resolved ✅ |
| `Utils/ReservedKeywords.cs` (static class with `static readonly` strings) | moved to `Variables/Reserved.cs` with `const string` everywhere; `Keywords` getter rewritten to scan literal fields. Single caller (`RegisterStartupParameters.cs`) swept ✅ |

## Deferred (cascading static-caller chain)

The brief listed several more sites, but each has a chain of *static
methods* that read the field. Converting the field to instance requires
converting every method that reads it to instance — and where those
methods are called from other static contexts (TypeMapping, etc.), the
chain cascades into refactors much larger than stage 16's scope.

| Site | Why deferred |
|------|--------------|
| `Callback/AskCallback.cs` `_options` | read by static `Deserialize` (used during snapshot rehydration); no instance available at that call site |
| `modules/http/providers/DefaultHttpProvider.cs` `_jsonOptions`, `_transportInOptions` | read by ~20 static helper methods that themselves don't have an instance reference; converting the methods cascades |
| `Choices/this.cs` `_gate`, `_registry` (the entire static class) | callers in `Utils/TypeMapping.cs` and `modules/builder/validateResponse.cs` reach through `App.Choices.@this.X` from static contexts. Converting Choices to instance breaks the static→static chain through TypeMapping |
| `Utils/PlangTypeIndex.cs` (entire static class) | same concern: `TypeMapping.GetType` (static) reaches `PlangTypeIndex.X`. Absorbing into `Types.@this` (instance) requires TypeMapping.GetType to take a context/app, which ripples to its callers. Out of stage 16's scope |

These are all real Rule C smells, but the eviction needs a coordinated
upper-level refactor (e.g., make `TypeMapping` instance-bound, or pass
context through). Tracked as carry-overs.

## Files changed

- `App/Data/this.Envelope.cs` (static → instance)
- `App/Channels/Serializers/Serializer/Plang/Data.cs` (static → instance)
- `App/modules/builder/providers/DefaultBuilderProvider.cs` (static → instance)
- `App/modules/llm/providers/OpenAiProvider.cs` (block deleted)
- `App/Utils/ReservedKeywords.cs` → DELETED
- `App/Variables/Reserved.cs` (NEW — const-everywhere, IsReserved/Keywords preserved)
- `App/Utils/RegisterStartupParameters.cs` (caller sweep)
- `Documentation/Runtime2/todos.md` (2026-05-07 OpenAi entry → RESOLVED)

## Verification

- `grep -rn "ReservedKeywords\." PLang/ PLang.Tests/ --include='*.cs'` → 0.
- `grep -rn "_requestCount\|MaxRequestsPerProcess\|RequestLimitExceeded" PLang/App/modules/llm/ --include='*.cs'` → 0.
- C# 2752/2752; PLang 199/199; build clean.
