# Stage 15 — coder plan (`compound-name-rename`)

Sweep Rule A renames: drop role-pattern suffixes that duplicate the
parent folder name.

## File renames (16)

| Today | After |
|-------|-------|
| `Cache/MemoryStepCache.cs` | `Cache/Memory.cs` |
| `Snapshot/ISnapshotted.cs` | `Snapshot/ISnapshot.cs` |
| `Data/PlangTypeConverter.cs` | `Data/Converter.cs` |
| `Channels/Serializers/TimeSpanIso8601Converter.cs` | `Channels/Serializers/TimeSpanIso8601.cs` |
| `Channels/Serializers/TypeJsonConverter.cs` | `Data/Json.cs` (cross-folder) |
| `Channels/Serializers/{Sensitive,Transport,View}PropertyFilter.cs` | `Channels/Serializers/Filters/{Sensitive,Transport,View}.cs` |
| `Channels/Serializers/Serializer/JsonStreamSerializer.cs` | `Channels/Serializers/Serializer/Json.cs` |
| `Channels/Serializers/Serializer/TextStreamSerializer.cs` | `Channels/Serializers/Serializer/Text.cs` |
| `Channels/Serializers/Serializer/PlangSerializer.cs` | `Channels/Serializers/Serializer/Plang/this.cs` (new subfolder) |
| `Channels/Serializers/Serializer/PlangDataSerializer.cs` | `Channels/Serializers/Serializer/Plang/Data.cs` |
| `Variables/Navigators/{JsonString,Dictionary,List,Object}Navigator.cs` | `Variables/Navigators/{JsonString,Dictionary,List,Object}.cs` |

Class names + namespaces inside each file updated to match.

## Caller-side updates

Sweep ~49 caller files via sed. Old names replaced with fully-qualified
`global::App.X.Y` form to avoid ambiguity.

## Subtle collisions resolved

- `App/Utils/Json.cs:63` — sed produced double-prefix; manually fixed.
- `Filters/View.cs` — class `View` collides with the `App.View` enum.
  Internal references qualified as `global::App.View.X`.
- `Plang/Data.cs` — class `Data` shadows `App.Data` namespace from inside
  itself; `Data.@this` / `Data.Type` references replaced with
  `global::App.Data.@this` / `global::App.Data.Type`.
- `Debug/this.cs:656` — manual sweep of the qualified
  `App.Channels.Serializers.SensitivePropertyFilter` (the regex hit only
  bare names).
- GlobalUsings: deleted the now-broken `MemoryStepCache` alias entirely
  (sed mangled it; the architect's plan had it removed too — class is
  just `App.Cache.Memory` now).

## Rule B / `Filters/this.cs` parent collection

Architect proposed a parent collection class. The three filter classes
are `static class` utilities (no state, no instances) — the methods are
called as `Sensitive.Mask`, `Transport.Strip`, `View.For`. A Rule B
collection (`@this` with instance properties) doesn't fit the existing
shape. Skipping for now — the *folder* groups them; introducing an
instance wrapper around static utilities would be ceremony without
benefit.

If a future stage makes filters stateful or wants a registry, that's
when the collection class earns its keep.

## Verification

- C# 2752/2752; PLang 199/199.
- `grep -rn "MemoryStepCache\|ISnapshotted\|PlangTypeConverter\|*PropertyFilter\|JsonStreamSerializer\|TextStreamSerializer\|PlangSerializer\|PlangDataSerializer\|*Navigator\.cs" PLang/ PLang.Tests/ --include='*.cs'` — only `INavigator` and `ValueNavigators` remain (intentional per brief).

## Out of scope

- `INavigator.cs` (interface keeps the suffix per brief).
- `ValueNavigators.cs` (plural-form, multiple navigators in one file).
- `UnregisteredMimeType.cs` (typed exception, conventionally compound).
