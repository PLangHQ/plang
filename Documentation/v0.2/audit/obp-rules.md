# OBP Audit Rules — Sharpened Detection

Five detection rules with grep recipes for finding OBP shape smells in `PLang/app/`. Each rule has a screen (the raw grep), a filter (what to discard), and worked examples from this codebase. Use after a major refactor to catch what slipped through, or when investigating a specific suspected smell.

For the foundational *principles* (what good OBP shape looks like), read `Documentation/v0.2/good_to_know.md` "OBP Naming Principle" and "OBP Smell Checklist", and `/shared/bots/obp/core.md` for the consolidated reference.

---

## Rule A — Compound class names

**Principle.** A class named `{Noun}{RolePattern}` is wrong because the role-pattern suffix is *behavior described as a class*. The role belongs to the noun, not to a separate type.

- The plural noun *is* the registry (`Channels` IS the channel registry — no `ChannelRegistry`).
- The singular noun *is* the entity (`Actor`, not `ActorEntity`).
- Suffixes that almost always indicate a wrong-shape name: `Manager`, `Helper`, `Service`, `Handler`, `Loader`, `Holder`, `Wrapper`, `Container`, `Dispatcher`, `Builder`, `Coordinator`, `Controller`, `Mediator`.

**Quick screen.**

```bash
grep -rEn "^\s*(public|internal|private|protected)?\s*(sealed\s+|static\s+|partial\s+|abstract\s+)*(class|record)\s+[A-Z][a-z]+[A-Z]" \
  PLang/app/ --include='*.cs'
```

Two capital letters in the class name is the red flag.

**Filter recipe (apply by hand or pipe through additional greps):**

1. **`XxxAttribute`** — C# convention; attribute classes must end in `Attribute`. Skip these: `grep -v ': Attribute'`.
2. **Per-impl variants** — `XxxNavigator`, `XxxProvider`, `XxxSerializer`, `XxxConverter`, `XxxStore`. The folder names the abstraction (`Navigators/`, `Providers/`); the prefix names the implementation. These are OBP-correct — same shape as `Cache/Memory.cs`. Skip when the file lives in a folder that names the abstraction.
3. **Tri-valent compound nouns** — `MemoryStepCache`, `SqliteSettingsStore`. When the impl AND the entity AND the role are all genuinely part of the identity, accept the compound. Rare.

**Today's count on `PLang/app/`:** ~145 raw hits → ~30-40 real candidates after filtering.

**Worked examples currently in the cleanup plan:**

- `app/Catalog/ExampleRenderer.cs` — `Renderer` is a role suffix. Rule A flags. Fix (stage 9): becomes `app/modules/Schema/Render.cs` (file name) on a navigable property `app.Modules.Schema.Render(...)`.
- `app/modules/cache/MemoryStepCache.cs` — three nouns (Memory + Step + Cache). Rule A flags. Fix (stage 14): `app/modules/cache/Memory.cs` (impl variant under the Cache abstraction).

---

## Rule B — `Get<Plural>()` returning a list

**Principle.** A method named `GetXs()` returning a list of X tells the architect there should be an `Xs` `@this` that *is* the list. The method shape hides what should be a navigable property.

Refinement: `Get(uniqueKey)` returning **one item** is fine (`Variables.Get(name)`, `app.Goals.Get(name)`). The smell is `Get*()` returning a **list** — the list as data, the filter/query verbs as methods on the collection.

**Quick screen.**

```bash
grep -rEn "Get[A-Z][a-z]+s\(" PLang/app/ --include='*.cs' \
  | grep -vE "GetType\(\)\.Get|GetInterfaces\(|GetProperties\(|GetMethods\(|GetParameters\(|GetFields\(|GetTypes\(\)|GetCustom|GetBytes\(|GetFiles\(|GetNames\(\)|Encoding\."
```

The unfiltered grep returns ~94 hits; almost all are .NET reflection (`type.GetProperties(...)`, `GetType().GetInterfaces()`) or framework I/O (`Encoding.GetBytes`, `Directory.GetFiles`, `Enum.GetNames`) — not the smell. The filter drops to ~5-8 real candidates.

**Today's count on `PLang/app/`:** 94 raw → ~5-8 real candidates.

**Worked examples currently in the cleanup plan:**

- `app/modules/this.cs:103` — `public IEnumerable<string> GetActions(string module)` flags. Fix: `app.Modules.<module>.Actions` (or similar) navigable.
- `app/modules/this.cs:384` — `public List<Data.@this>? GetDefaults(string module, string actionName, HashSet<string>)` flags. Same fix shape.
- `app/channels/channel/this.cs:179,188` — `Events.GetBindings(type)` flags. The bindings should be a navigable collection on Events.

---

## Rule C — Static fields are a missing `@this`

**Principle.** A `static` field — including `static readonly` to a mutable collection — has no `@this` owner. The data is process-global with no place to belong. Hand the field to the owning `@this` (App, or one of its children).

This rule covers **fields, not methods.** Static factory methods, conversion operators, and helpers (`static @this Ok(...)`, `static implicit operator string(Variable v)`) are behavior, not state, and stay.

Three exceptions for state:

1. **`const`** — compile-time constant, no allocation, no instance.
2. **`AsyncLocal<T>`** — flow-scoped, not process-global. Different mechanism, different problem.
3. **Lock objects whose guarded data is itself irreducibly static** — and on this codebase, that set is empty. If you reach for a static lock, the data it guards should move first; the lock follows.

**Quick screen — fields only.**

```bash
# Static fields with assignment or termination, not methods
grep -rEn "^\s+(public|private|internal|protected)\s+static\s+(volatile\s+)?[A-Za-z_<>?,\.\[\]\s]+\s+_?[a-zA-Z]\w*\s*[=;]" \
  PLang/app/ --include='*.cs' \
  | grep -vE "static\s+(class|partial|implicit|explicit|event|async|readonly)" \
  | grep -v "("

# Static events (separate — they're process-global subscription points)
grep -rEn "^\s+(public|private|internal|protected)\s+static\s+event" PLang/app/ --include='*.cs'

# Mutable static-readonly collections (the deeper smell — reference is readonly but contents mutate)
grep -rEn "^\s+(public|private|internal|protected)\s+static\s+readonly\s+(List|Dictionary|HashSet|ConcurrentDictionary|ConcurrentBag|ConcurrentQueue)" \
  PLang/app/ --include='*.cs'
```

**Today's count on `PLang/app/`:** ~7 mutable static fields, 1 static event, ~9 mutable static-readonly collections. ~17 real targets total.

**Worked examples currently in the cleanup plan:**

- `app/Utils/PlangTypeIndex.cs:27,37` — `_initialized`, `_clrTypeFullNamesInitialized` flags. Fix (stage 15): become instance state on `app/types/this.cs`; the locks vanish because construction is deterministic at App.Start.
- `app/modules/llm/providers/OpenAiProvider.cs:41` — `private static int _requestCount` flags. Fix (stage 15): delete (per-process counter on an actor-resolved provider — wrong scope; per Ingi's review: temporary blocker, not worth promoting).
- `app/modules/test/run.cs:30` — `internal static event Action<app.@this>? ChildAppCreated` flags. Fix (stage 15): test-runner registry pattern (design TBD).

---

## Rule D — Gerund/verb-named app-graph properties and folders

**Principle.** A property on `app.X` should name an **object you hold and navigate**, not a state or an action. Gerunds (`-ing` endings) describe activity; verb roots (`Build`, `Run`, `Parse`) describe commands; nouns name the thing. `app.Building` reads "the system is currently building" — state. `app.Build` reads "build (verb)" — command. `app.Builder` reads "the thing that builds" — object.

CLI follows: flag form moves to the noun (`--builder`, `--tester`). Verb commands stay verbs because commands *are* actions (`plang p build`, `plang p test`).

Three forms must all agree:

| Form | Today (wrong) | After (correct) |
|------|---------------|-----------------|
| Folder | `app/Build/` | `app/modules/builder/` |
| App property | `app.Building` *(or `app.Build` if it's the type form)* | `app.Builder` |
| CLI flag | `--build` | `--builder` |
| CLI command | `plang p build` | unchanged (verb) |

**Quick screen — two parts.**

1. **Property side (gerund form):**

```bash
grep -rE "(public|internal)\s+\w+ing\b" PLang/app/this.cs
```

Catches `public Testing Testing { get; }`. Read each: a real state name is the rare case; rename otherwise.

2. **Folder side (verb-root form):**

```bash
ls PLang/app/ | grep -E "^(Build|Test|Run|Parse|Render|Compile|Stream|Process|Setup|Init|Load|Save|Read|Write|Send|Receive)$"
```

Catches `app/Build/`, `app/Test/` — folders named with verb roots that read as commands, not objects.

**Both screens needed.** The "three forms must all agree" rule means a misshapen folder, property, or CLI flag are all symptoms of the same smell; checking only one form misses the others. `app/Build/` slipped past the gerund-only screen during the v1 audit; the folder screen catches it.

**Today's count on `PLang/app/`:** 1 gerund property (`Testing`), 2 verb-root folders (`Build/`, `Test/`).

**Worked examples currently in the cleanup plan:**

- `app/this.cs:188` `public Testing Testing { get; }` — Rule D flags (gerund). Fix (stage 16): `app/tester/`, `app.Tester`.
- `app/Build/` folder + `app/this.cs:194` `public global::app.Build.@this Build { get; }` — Rule D flags (verb root). Fix (stage 16): `app/modules/builder/`, `app.Builder`.

---

## Rule E — Decomposed parameters that should navigate

**Principle.** A method `B.X(spec, modules)` where `modules` is reachable from `B` (or from `B.Parent.Modules`, or from `spec.Owner.Modules`, or any other navigation chain rooted at the receiver) is a decomposition smell. The caller is being made to chop its own children off and pass them in; the OBP form is **the callee navigates the receiver for what it needs**.

Two side wins of the navigation form:

1. **Owner is forced explicit.** To navigate, the method has to live where its data lives. Decomposed-parameter methods can live anywhere; navigated methods can only live on a node that *has* the data they need. This makes Smell #4 ("allocate-here / mutate-there / cleanup-elsewhere") harder to introduce by accident.
2. **API surface stops leaking caller structure.** `Render(spec, modules)` is two parameters wide; `Render(spec)` is one. Renames and refactors of the navigation chain don't change the public method signature.

**Quick screen.**

```bash
grep -rnE "\.\w+\(.+\.App\.\w+" PLang/app/ --include='*.cs' \
  | grep -v "//\|Console\|Debug.Write"
```

Surfaces every call site where the caller is reaching into `App.X` to pass it as a parameter. Each is a candidate.

**Refinement.** Not every parameter is decomposed. A method that takes data the receiver *cannot* navigate to (a fresh value computed by the caller, an opaque token, an unrelated entity) is correctly parameterized. The smell is specifically *parameter is a child of the receiver* or *parameter is reachable through the receiver's parent chain*.

**Today's count on `PLang/app/`:** ~4 raw hits, 2 real smells.

**Worked examples currently in the cleanup plan:**

- `app/module/builder/providers/DefaultBuilderProvider.cs:37` — `Catalog.@this.Build(action.Context.App.Module)` flags. Fix (stage 9): `app.module.Schema.Build()` instance method, navigates `this.Module` internally.
- `app/Catalog/ExampleRenderer.cs:Render(spec, modules)` flags. Fix (stage 9): instance method `Schema.Render(spec)` navigating `this.Modules`.

---

## Notes on rule effectiveness

The 5 rules together catch about a quarter of the cleanup stages mechanically (stages 9, 14, 15, 16, partial 17). The other ~12 stages need the four CLAUDE.md foundational smells plus architectural reading — those are *patterns you spot in code*, not greppable as quick screens. See `Documentation/v0.2/good_to_know.md` "OBP Smell Checklist" for the foundational 4.

Rules A and B are noisy (need the filter recipes); Rules C, D, E are sharper. Rule D needs **both** screens — property-side and folder-side — because the smell expresses across three forms and a one-form screen misses two of them.

These rules are *finders*, not *fixers*. Each hit is a candidate for design discussion — read the code, decide whether the smell is real, then design the fix.
