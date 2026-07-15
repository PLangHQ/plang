# Stage 4 — coder comment round (read + trace, no code)

Traced against `1e5fa13c8`. Answers to the 6 spots in `architect/code-draft.md`, plus one finding that reframes the whole teaching section.

## The finding that reframes model #6 — teaching is ALREADY 100% Fluid

The plan says "`Describe()` — imperative reflection assembling prompt text in C#" and model #6 wants to move text assembly into templates. **The text assembly is already in templates.** `Describe()` builds *structure*, not text. Four Fluid templates render it:

| Template | Rendered by | Reads off the action |
|---|---|---|
| `os/system/actions/v2/summary.planner.md` | `builder/BuildGoal/Plan.goal:7` | `a.Module`, `a.ActionName`, `a.Description`, `a.ModuleDescription`, `a.Examples`, `a.IsModifier` |
| `os/system/actions/v2/summary.md` | (renderer — full param pass) | + `a.Parameters` (`p.Name`, `p.Value`), `a.ReturnTypeName`, `a.Cacheable` |
| `os/system/builder/llm/templates/stepActionDetails.template` | `builder/BuildStep/Start.goal:22` | `a.Parameters` (`p.Name`,`p.Value`), `a.DescriptionRendered`, `a.NotesRendered`, `a.ExamplesMdRendered`, `a.Cacheable`, `a.ReturnTypeName` |
| `os/system/actions/summary.md` | (v1, legacy?) | `a.Parameters` (`p.Name`,`p.Value`), `a.Cacheable` |

So the ONLY genuine C# text composition left in the teaching path is **one line**: the per-parameter `desc` string at `module/this.cs:341-352` —

```csharp
var desc = hasVar ? "%var%" : typeName;          // "%var%" | "string?" | "operator"
if (defaultAttr != null) desc += $" = {FormatDefault(defaultAttr.Value)}";
parameters.Add(new data.@this(prop.Name, desc, context: App.System.Context));
```

Templates then just lay `{{ p.Value }}` (= `desc`) out. **That single `desc` composition is what moves into the templates** when `data.@this` param-bags become `property` rows (`p.IsVariable`, `p.Type.Name`, `p.Nullable`, `p.Default`) — exactly the draft's `modules.md` shape.

### What Stage 4's teaching work ACTUALLY is (narrower + more precise than "kill C# text assembly")
1. Swap the data source the 4 templates read: `Describe()`→`StepActions` of `data.@this` bags ⟹ `app.module` → `module.Actions` → `action.Properties` of `property` rows.
2. Move the one `desc` composition (`module/this.cs:341-352` + `FormatDefault` + `IsVariableNameSlot`) OUT of C# INTO each template's param loop.
3. Rewrite **all four** templates' `{{ p.Value }}` loops to read the structured row fields.
4. Prose: the stuffed `DescriptionRendered`/`NotesRendered`/`ExamplesMdRendered`/`Description`/`ModuleDescription` fields become element prose doors; templates read `action.Description()` etc.

**Grep gate correction:** there is no C# StringBuilder assembling the catalog to delete — the demolition list's "every C# prompt-text assembler in the teaching path" resolves to exactly `module/this.cs:341-352` (`FormatDefault`, `IsVariableNameSlot`) + the `Describe()` structure builder. Don't hunt for more; there isn't more.

## Q3 — the parity gate capture point (this is the important one)

The draft frames it as "render the builder prompt via `Describe()` and via the template; assert equality." **There is no 'render via Describe()' seam — `Describe()` returns structure, the templates render.** So the gate can't be "two renderers."

Correct capture point: **the rendered template OUTPUT strings** — pin a golden of today's `%actionSummary%` / `%actionDetails%` (the actual strings handed to the LLM), one per template:
- `Plan.goal` → `%actionSummary%` (planner.md)
- `BuildStep/Start.goal:22` → `%actionDetails%` (stepActionDetails.template)
- whoever renders `v2/summary.md` (full param pass — I could NOT find a goal rendering it; **verify who renders `summary.md` vs `summary.planner.md`**, it may be dead or C#-side)

Then after the swap: rewritten-templates(new `property` rows) **byte-identical to** golden. Because the `desc` composition moves C#→template, the gate is really proving *"the template can reconstruct `desc` from the row fields."* Golden must cover: module w/ prose, module w/o, `[Code]` action, choice param, **a nullable param, and a param with a `[Default]`** (those two exercise the `?`/` = x` reconstruction that's the actual risk).

## Q1 — hot-path check on the moved queries: SAFE

`IsModifier` / `GetModifierOrder` / `GetActionType`:
- `IsModifier` + `GetModifierOrder` are called ONLY from `Actions.GroupModifiers` (`goal/steps/step/actions/this.cs:149,173`), reached via `GroupModifiersRecursive`. Both callers are **build-time**: `goal/this.cs:325` ("Called before .pr serialization") and `build/code/Default.cs:265`. At runtime modifiers are already grouped in the `.pr`. **Not a run hot path.**
- `GetActionType` callers are all build/type-view/test paths (`type/list`, `type/spec/render`, `build/code/Default`, `getTypes`, `test/discover`, `BuildResponse.Validate`). None on the per-step run path.

Moving these onto the action element does NOT touch a hot path. No index-level fast door needed.

## Q4 — `Cacheable`'s two owners: there's really ONE owner, plus dead code

- **Single source of truth:** the `[Action(Cacheable=false)]` attribute (`module/Attributes.cs:13`, ~55 actions set it).
- `action.@this.Cacheable` (`action/this.cs:44`, `new bool`) is stuffed from the attribute at `module/this.cs:434` during `Describe()`, is **`[JsonIgnore]` (NOT `.pr`-stored — the draft's "`.pr`-stored Cacheable" is wrong)**, and has **zero runtime readers** (only the templates read `a.Cacheable` for the `[no-cache]` tag).
- `module/this.cs:171 IsCacheable(module, actionName)` — **zero callers, dead code.**

**Single owner = the attribute, read lazily on the action element** (an `action.Cacheable` accessor doing `ParameterSchema.GetCustomAttribute<ActionAttribute>()?.Cacheable ?? true`). Delete `IsCacheable` and the stuffed `[JsonIgnore]` field; templates read the accessor. This is a flat-copy smell today (attribute → mirrored onto a transient field).

## Q5 — the two deliberate behavior changes

- **Choice registration on App-attach + inline:** confirmed real latent gap. `RegisterModuleChoiceTypes` runs once at `app/this.cs:307` (boot). A `code.load` module registered after boot never gets its closed set / `Reader<T>`. Firing inline on `Register`/`RegisterType` fixes it. No consumer breaks — it only ADDS registrations that should already exist. ✅ do it.
- **Cached-elements vs rebuild-per-`Describe()`:** today `Describe()` rebuilds the whole catalog every call (`Default.cs:24` pays it per build call; the token-scan at `Default.cs:77` pays it AGAIN). Caching on the element, invalidated by registry mutation, is strictly better and mutation-safe. ✅.

## Q2 — Fluid-over-hosts spike: AGREE, spike first

Endorsed — but note the templates ALREADY enumerate `a.Parameters` (a `List<data.@this>`) and `a.Examples` today and work, so Fluid reflecting over our objects isn't wholly unproven. The NEW risk is specifically: (a) the `property` row host, (b) `module.Actions`/`action.Properties` returning the **native plang `item.list`** (the exact member type that broke the 3 Modules tests when `item.list` went public), and (c) **async prose doors** — Fluid must `await` `action.Description()`. Spike a single module element through `render` covering all three before 4d. The async-member-accessor is the least-proven bit; if Fluid can't await a `Task<string?>` door, prose must be pre-resolved into a sync property at mint (which reintroduces a stamp step — flag early).

## Q6 — order + a scope note

- 4g (error templates) and 4f (test report) are independent of 4a–4e; agree they can land anytime after the template machinery exists.
- **`GetChannelInventory` (module/this.cs:227): its ONLY caller is a test** — `PLang.Tests/Wire/App/ChannelsTests/Stage4_BuilderCatalogTests.cs:27`. No production caller. So "dies outright, callers → `actor.Channel`" is right; concretely, repoint that one test to `app.User.Channel.ChannelNames` (or a test extension). It's already production-dead.
- **4a is a real split, confirmed:** `module/this.cs` today = the collection (`_modules`, `Discover`, `Register`/`RegisterType`, `Remove`/`Clear`, `DisposeAsync`, `GetCodeGenerated`, `Schema`) + teaching (`Describe`, `GetAllTypesInNamespace`, `FormatDefault`, `IsVariableNameSlot`, `CapabilityInterfaces`, `MarkdownTeaching*`) + per-action queries (`GetActionType`, `IsModifier`, `GetModifierOrder`, `IsCacheable`, `GetDefaults`). The split is clean; teaching + queries shed, collection relocates. Not bigger than it reads.

## Net recommendation
Land order fine. The one framing correction that matters: **model #6 is ~90% already true** — don't scope "kill C# text assembly" as a big hunt; it's the one `desc` line at `module/this.cs:341-352` moving into 4 templates. The parity gate's real job is proving those 4 rewritten templates reconstruct today's exact output from `property` rows — capture goldens at the rendered-string seam, not at `Describe()`.
