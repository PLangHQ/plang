# Born-whole execution map (Phase 2/3) — ready to run once the meta-type bootstrap lands

Phase 1 (facts fix) is DONE and green: a type built from a CLR class reads its `[Format]` into its
kind (`type/this.cs` `KindOfClass` + the clrType ctor rebuild; `kind/empty` carries `_format`;
`image` Type override passes the kind name). Types suite 32/0.

## INGI'S RULING (A, in 3 green steps — decision 887)
Every type and kind per app (884 holds). Cost-collapser: a number op is a DERIVED birth (885) — `a+b` takes its kind from `a`'s own type, already app-owned, so ops DON'T need the app; only ORIGIN births do, and those go through Make (880).
- STEP 1: values hold the type they're born with (`: base(type)`, set by Make/Create(raw,type), or forwarded from the value they came from). Overrides go for MOVED types; not-yet-moved still compiles (base keeps a temp parameterless + `Type => _type ?? rebuild`). Reds 1&2 (facts fix) + red 3 (element-kind ClrType) carry in. Commit, C# green for what it touched, report hash.
- STEP 2: kinds move into each app's list (number's 15 precisions, empty kinds, json/dict/list, schemes). A number reaches its precision kind through its own type. LIST the static kind constants + uses in the step report. Commit, green, report hash.
- STEP 3: base ctor becomes REQUIRED; the parameterless stand-in + context-free origin ctors + INTO operators go; nothing made without its type. Commit, green, report hash.
Each step green against runtime2, report before the next. DONE WHEN step 3 done, all reds+guards green, surface re-pinned under 867/880/883/884/887.

## STATUS
- Phase 1 facts fix landed in-tree (uncommitted): KindOfClass in type/this.cs, kind/empty format param, image Type override → kind name. Reds 1&2 GREEN through the app's Create door. KindOfClass is thread-safe (IdentityOf = ConcurrentDictionary).
- FLAKE to flag to tester: ImageKindKnowsItsMimeTests + TextKindIsTextByItselfTests both use `new app.@this("/app")` (same root) → intermittent under TUnit intra-suite parallelism. Test-isolation, not a production race.
- Red 3 (AKindedSlot) needs born-whole (the element kind IS the registered action type, carrying ClrType).

## Architect's ruling on the meta-type (decision 884): ONE self-typed root
- The app's type list holds ONE root — the "type" type — whose type is ITSELF (ctor sets `_type = this`).
- Every type and kind the list makes is born holding that root; THE LIST PASSES IT IN.
- type.@this and kind.@this forward the root via `: base(root)` — SAME base ctor as the 28 value types. No Type override, no rebuild, no parameterless fallback beyond the root's self-type.
- No serialization blocker: types are read via `app.type.list[name]` (which has the root), NOT STJ-deserialized via [JsonConstructor].

## Base `item.@this`
- `private readonly type.@this _type;`
- `protected @this(type.@this type) => _type = type;` (all 28 value types + type/kind forward through this)
- root bootstrap (ONE instance): `private protected @this(RootMarker _) => _type = (type.@this)this;` + `private protected readonly struct RootMarker { }` — marker keeps it from being an implicit base call, so a missing `: base(type)` is a compile error, not a runtime cast crash.
- `protected internal global::app.type.@this Type => _type;` (NON-virtual; was the rebuild at :422)

## Root threading (type.@this / kind.@this / type.list)
- type.list: `_root` field, built once in Load via type.@this's root ctor; passed to every type/kind the list makes.
- type.@this: a root ctor (self-typed, list-built) + all other ctors forward a `type.@this root` to `: base(root)`. ~12 internal type-construction sites (list/Registry) pass `_root`. KindOfClass passes the constructing type's root to the kind.format<T> it builds.
- The ~28 EXTERNAL `new type.@this(...)` (value classes) DISAPPEAR under born-whole — values get their type from Make / this.Type / the list, not `new`.
- static `type.@this.Null` (2 uses, both have list/context) → the list's per-app null type.

## Per-subtype: the type each forwards to `: base(TYPE)`, delete its `Type` override
| type | TYPE to pass | friction |
|---|---|---|
| base64 | `new("base64", typeof(@this), Kind)` | `Kind` is init-prop set AFTER ctor (`{ Kind = … }`) → move Kind into ctor |
| binary | `new("binary", typeof(@this), Kind)` | same `{ Kind = … }` → ctor param; `Kinded` rebuilds |
| bool | `new(typeof(@this))` | singleton True/False (Ingi: the bool type's own values) |
| choice<T> | `new(typeof(@this<T>), _set.Name)` | `_set` static, context-free OK |
| computed | `_declared==null ? new(typeof(@this), _declaredKind) : new(_declared, _declaredKind)` | has the fields |
| content (file/url base) | `new(GetType()){ kind=_kind, Template }` → `new(GetType(), _kind?.Name){ Template }` | _kind from path ctor (has ctx) |
| culture | `new(typeof(@this))` | singleton Machine (culture type's own) |
| date/datetime/time/guid/tag | `new(typeof(@this))` | no kind |
| dict | `new(typeof(@this)){ Template }` | Template via initializer |
| duration | already caches `_type` = `new("duration", typeof(@this), kind.Name)` | forward it |
| image | `new("image", typeof(@this), _kind?.Name)` | DONE as override; born-whole forwards |
| list (non-generic) | `new(typeof(@this)){ Template, kind=_kind }` | Template+kind via init |
| list<T> | `base.Type`-conditional → element-tag `new(typeof(list.@this), NameOf(T))` | generic |
| note / note.line | `new(typeof(@this))` | built by note.@this |
| null | `_declared ?? <null type>` | per-app (Ingi); Instance/Absent REMOVED — each use gets its type from context |
| number | `new("number", typeof(@this), Kind.Name)` | Kind carried in private ctor; 15 ops removed |
| path (file/url) | `new("path", typeof(@this), Scheme)` | Scheme abstract per subclass |
| prose | `new("text", typeof(text.@this))` | renders to text; context-free C# helper |
| separator | `new("text", typeof(@this), "separator")` | subclasses text |
| setting | `GetType()==@this ? new(NameOf(@this), typeof(@this)) : new(NameOf(GetType()), GetType())` | class-as-type |
| source | already holds `_type` whole | forward it |
| text | `new("text", typeof(@this), Kind, template: Template)` | Kind init-prop → ctor param; derived (ToUpper/Trim/…) forward `this.Type`; operator `(text)string` REMOVED |

## INTO-plang implicit operators to REMOVE (OUT-to-CLR ones stay)
- number: 15 (`@this(sbyte)…@this(decimal)`, this.cs:86-100) — arithmetic results forward operand kind
- text: `@this(string)` (:388)
- bool: `@this(bool)` (:35)
- guid: `@this(Guid)` (:61); duration: `@this(TimeSpan)` (:89)
- binary: `@this(byte[])` (:205); base64, choice<T> `@this<T>(T)` (:46)
- size/tag/status/variable/content — check each file's INTO op
Compiler lists every external caller → fix each: `context.Ok(x)` / `type.list[name].Make(x, ctx)` / forward `this.Type`.

## Singletons (Ingi ruled)
- null: per-app; remove `Instance`/`Absent` constant; each use reaches the null type through its data/context
- bool True/False: the bool type's own values; setting field-defaults → `[Default(false)]` made at the setting's birth through its type (pattern: `goal/step/action/this.Build.cs:112 declared.Type.Make(declared.Default, context)`); `call/list/this.cs:62` via context
- culture.Machine: the culture type's own via setting's birth (`setting/this.cs:24`)
- given.Instance: the given type's own value (`permission/this.cs:54`)

## Collapses (Phase 3)
- re-resolves GO: `type.Get:886`, `type.Output:63`, `type.Setting:900` (`list[this, ctx]`)
- floor GO: `source.cs:115` drop `_type.Name is "text"` (empty kind now IsText via [Format])
- `source.cs:240` → (c) the declared type's birth refuses (not a Name list)
- `type/this.cs:754` reuse the family's registered empty kind (not a fresh empty)
- action-slot by ClrType not Name: `action/this.Validate.cs:54-55`, `formal/reader.cs:221,:349`, `serializer/Reader.cs:153-154` → `slot.Type.ClrType == typeof(action.@this)` (and list<action> via kind)
- bare-type makers → list doors (12 sites, scattered map); the rerouted test's kinded method `new type("list","action")` → list name+kind door
- NOT stage 1: `formal/reader.cs:317`/:415 (builder lexer), `source.cs:372` (stage 2 sync Write)

DONE WHEN: reds green (ImageKindKnowsItsMime ✓, TextKindIsTextByItself ✓, the kinded action-slot method, 3 kind/strict plang), guards green, no new reds, surface re-pinned under 867/880/883.
