# Stage 15: `compound-name-rename`

**Read first:**
- `plan/principles.md` — **Rule A** (compound class names) and the sub-rule (role suffix duplicates parent folder).
- `plan/scope-map.md` — no scope changes; pure rename + relocation work.
- `plan/post-cleanup-tree.md` — the destination tree shows where each rename lands.

**Goal:** Sweep all Rule A renames identified by the two-capital screen. Each is mechanical: drop suffix that duplicates the parent folder; rename to the noun. Plus a few cross-folder relocations and one new collection (Rule B).

**Scope:**
- *Included:* the rename list below — 13+ file renames, one new collection (`Filters/this.cs`), namespace updates, caller sweeps.
- *Excluded:* renames that earlier stages already did (Test* prefix drops in Tester/, Catalog renames in Modules/Schema/, etc.).
- *Excluded:* typed exceptions (`UnregisteredMimeType` stays — conventionally compound).

**Deliverables:**

### Per-file renames

| Today | After | Reason |
|-------|-------|--------|
| `App/Cache/MemoryStepCache.cs` | `App/Cache/Memory.cs` | role suffix dup; folder = Cache |
| `App/Snapshot/ISnapshotted.cs` | `App/Snapshot/ISnapshot.cs` | past-participle interface naming awkward (matches IDisposable convention) |
| `App/Data/PlangTypeConverter.cs` | `App/Data/Converter.cs` | "PlangType" prefix dup; namespace = App.Data |
| `App/Channels/Serializers/TimeSpanIso8601Converter.cs` | `App/Channels/Serializers/TimeSpanIso8601.cs` | "Converter" suffix dup; folder = Serializers |
| `App/Channels/Serializers/TypeJsonConverter.cs` | `App/Data/Json.cs` (RELOCATED) | converter for `Data.Type`; lives where the type lives |
| `App/Channels/Serializers/SensitivePropertyFilter.cs` | `App/Channels/Serializers/Filters/Sensitive.cs` | folder + suffix drop (Property + Filter) |
| `App/Channels/Serializers/TransportPropertyFilter.cs` | `App/Channels/Serializers/Filters/Transport.cs` | same |
| `App/Channels/Serializers/ViewPropertyFilter.cs` | `App/Channels/Serializers/Filters/View.cs` | same |
| `App/Channels/Serializers/Serializer/JsonStreamSerializer.cs` | `App/Channels/Serializers/Serializer/Json.cs` | "StreamSerializer" suffix dup |
| `App/Channels/Serializers/Serializer/TextStreamSerializer.cs` | `App/Channels/Serializers/Serializer/Text.cs` | same |
| `App/Channels/Serializers/Serializer/PlangSerializer.cs` | `App/Channels/Serializers/Serializer/Plang/this.cs` (new subfolder) | `application/plang` transport gets its own subfolder for future Protobuf etc. |
| `App/Channels/Serializers/Serializer/PlangDataSerializer.cs` | `App/Channels/Serializers/Serializer/Plang/Data.cs` | sibling to Plang/this.cs |
| `App/Variables/Navigators/JsonStringNavigator.cs` | `App/Variables/Navigators/JsonString.cs` | "Navigator" suffix dup; folder = Navigators |
| `App/Variables/Navigators/DictionaryNavigator.cs` | `App/Variables/Navigators/Dictionary.cs` | same |
| `App/Variables/Navigators/ListNavigator.cs` | `App/Variables/Navigators/List.cs` | same |
| `App/Variables/Navigators/ObjectNavigator.cs` | `App/Variables/Navigators/Object.cs` | same |

(`INavigator.cs` and `ValueNavigators.cs` stay — `INavigator` is the interface (drop `Navigator` would conflict with the namespace), `ValueNavigators` is plural and contains multiple value-type navigators.)

### New file (Rule B)

`App/Channels/Serializers/Filters/this.cs` — parent collection for the three filters. Per Rule B (`Get<Plural>()` is a missing collection type), three same-shape filters earn a parent registry.

```csharp
namespace App.Channels.Serializers.Filters;

public sealed class @this
{
    public Sensitive Sensitive { get; } = new();
    public Transport Transport { get; } = new();
    public View View { get; } = new();
    // Or a registration-shaped surface if the existing pattern uses one
}
```

(Read existing `*PropertyFilter.cs` files to see the natural surface shape.)

### Class names inside renamed files

When you rename `MemoryStepCache.cs` → `Memory.cs`, the class inside also renames `MemoryStepCache` → `Memory`. Same for all entries above. The `: Interface` declarations stay.

### Namespace updates

Each renamed/relocated file: namespace declaration updates to match the new folder path. Cross-references `using App.X.Y;` update.

### Caller sweeps

After all renames:
- `grep -rn "MemoryStepCache\|ISnapshotted\|PlangTypeConverter\|TimeSpanIso8601Converter\|TypeJsonConverter\|*PropertyFilter\b\|JsonStreamSerializer\|TextStreamSerializer\|PlangSerializer\|PlangDataSerializer" PLang/ PLang.Tests/ Tests/ --include='*.cs'` — zero hits.
- `grep -rn "Navigator\b" PLang/App/Variables/Navigators/ --include='*.cs'` — only references in `INavigator.cs` (the interface name) and `ValueNavigators.cs` (the plural).

### Definition of done

- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2752/2752).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --tester` green from a fresh rebuild.
- All renames listed above are done.
- All namespace declarations match new paths.
- All callers compile (build break catches misses).

**Dependencies:** None on stage 16 specifically. Independent.

## Design

### The smell this closes

Rule A — compound class names. When the role-pattern suffix names the folder (Cache, Filters, Serializer, Navigators), the suffix duplicates the parent. Drop the suffix; the namespace already says it. Reading `App.Cache.Memory` is clear; reading `App.Cache.MemoryStepCache` is redundant.

The Filters collection (Rule B) closes a missing-collection: three same-shape filter classes need a registry parent.

### Files touched

**Renames + relocations:** ~16 files.
**New file:** 1 (Filters/this.cs).
**Modified for namespace + using updates:** every renamed file + likely 5-10 caller files.

### Risk + dependencies

**Risk: low-medium.** Mechanical rename work, but volume increases the chance of grep misses. Build catches misses.

Possible failure modes:
1. **Type-name collisions.** `Memory` (ex-MemoryStepCache) might collide with a type elsewhere. `Json`, `Text`, `Object`, `List` are common names; verify no collisions in adjacent namespaces. C# resolves via namespace; the FQN should disambiguate.
2. **The `Plang/` subfolder under Serializer/.** Two files (this.cs + Data.cs) move into a new subfolder. Verify the source-generator doesn't have hard-coded path assumptions for `PlangSerializer.cs` (unlikely; it works by attribute scanning).
3. **The new `Filters/this.cs` collection** — if no existing parent exists, the three filters' registration call sites need updating. Read how filters are registered today and update accordingly.
4. **`TypeJsonConverter` cross-folder relocation** to `App/Data/Json.cs`. Verify the file's `JsonConverter<Type>` shape works in the new namespace (`App.Data` instead of `App.Channels.Serializers`).

**Dependencies: none.**

### Tests

**No new tests.** Behavior preserved.

**Existing test coverage to verify:**
- Tests that exercise filters, serializers, navigators by class name — sweep the test file references.
- `Tests/` — full PLang suite.

### Watch for (coder eyes-on)

- **The Plang/ subfolder creation** — `mkdir App/Channels/Serializers/Serializer/Plang/`, then move PlangSerializer.cs → Plang/this.cs and PlangDataSerializer.cs → Plang/Data.cs.
- **The Filters/ subfolder creation** — `mkdir App/Channels/Serializers/Filters/`, move 3 files in, drop suffixes, add this.cs.
- **The TypeJsonConverter cross-folder move** — from Channels/Serializers/ to Data/. Different namespace.
- **Prior migration leftovers** — earlier stages may have left dangling references. Greps for `PlangType*`, `*PropertyFilter`, `*StreamSerializer` are the long pole.
- **`ValueNavigators.cs` plural-form** — stays; the file contains multiple value-type navigators (presumably StringNavigator, NumberNavigator, etc. inside one file). Don't split.
- **`INavigator.cs` interface name** — stays as `INavigator` despite being in `Navigators/` folder. Interfaces by convention drop the suffix but `I` prefix is the marker; `INavigator` reads natural.

### Stages that follow this one

- **Stage 16** (`static-state-eviction-sweep`) — same Tier 4 batch; independent.
- **Stage 19** (`provider-to-code-rename`) — biggest sweep; own session.

### Out of scope

- `UnregisteredMimeType.cs` (typed exception — conventionally compound).
- `EventContext` and `MigrationEnvelope` (already absorbed by the channels merge per earlier sessions).
- The Catalog/Spec renames (stage 9 already did them).
- The Test* prefix drops (stage 17 already did them).

## Commit plan

```
runtime2-cleanup stage 15: Rule A renames sweep — drop role suffixes that duplicate the folder

Per principles.md Rule A sub-rule: when a class name's role-pattern
suffix names the parent folder, drop the suffix — the folder already
says it. Plus a few cross-folder relocations + one new Filters/
collection (Rule B — three same-shape filters earn a registry parent).

File renames (16):
  Cache/MemoryStepCache.cs → Memory.cs
  Snapshot/ISnapshotted.cs → ISnapshot.cs
  Data/PlangTypeConverter.cs → Converter.cs
  Channels/Serializers/TimeSpanIso8601Converter.cs → TimeSpanIso8601.cs
  Channels/Serializers/TypeJsonConverter.cs → Data/Json.cs (RELOCATED)
  Channels/Serializers/{Sensitive,Transport,View}PropertyFilter.cs
                          → Filters/{Sensitive,Transport,View}.cs
  Channels/Serializers/Serializer/JsonStreamSerializer.cs → Json.cs
  Channels/Serializers/Serializer/TextStreamSerializer.cs → Text.cs
  Channels/Serializers/Serializer/PlangSerializer.cs → Plang/this.cs (NEW SUBFOLDER)
  Channels/Serializers/Serializer/PlangDataSerializer.cs → Plang/Data.cs
  Variables/Navigators/{Json,Dictionary,List,Object}Navigator.cs
                          → drop Navigator suffix (4 files)

New file:
  Channels/Serializers/Filters/this.cs (Rule B parent collection
                                        for the 3 filters)

Class names inside files renamed to match. Namespaces updated. Caller
sweeps via grep on the old names.

Out of scope: UnregisteredMimeType (typed exceptions stay compound),
INavigator and ValueNavigators (interface convention / plural-form).
```
