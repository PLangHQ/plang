# OBP Cleanup — running collection

A backlog of **OBP shape violations** found during other work, parked for a *dedicated* pass rather than fixed inline (so focused feature branches stay focused and green). Append as you find them; fix in their own branch.

Rule of thumb for whether something lands here vs. gets fixed now: if fixing it is a **wide, cross-cutting refactor orthogonal to the branch you're on** (many callers, touches a shared registry), park it here. If it's local to what you're already changing, just fix it.

Reference: `Documentation/v0.2/object_pattern_formal.md` (the formal pattern), `Documentation/v0.2/good_to_know.md` "OBP Smell Checklist".

Each entry: **location · the smell · the OBP-clean target · status · found-in**.

---

## 1. `app.type.list.@this` — the type registry has accreted a wide verb/`Get` surface

**Location:** `PLang/app/type/list/this.cs` (`app.type.list.@this`).

**Found-in:** `type-kind-strict` (2026-05-31), while reviewing `BuildTypeEntries` usage.

**Status:** open — do NOT do it in `type-kind-strict` (coder is actively editing these files; it's orthogonal to the type/kind model). Its own branch after type-kind-strict lands.

**The smell** (the element types `image`/`number`/`path`/`hash` are clean — this is concentrated in the registry):

- **Collection-proxy verbs** — methods that hand back a collection the registry should simply *be/expose* (OBP: "Collections are the API; expose the collection, don't proxy it"):
  - `BuildTypeEntries(modules)` — a **verb proxying a collection**, and the name "TypeEntries" **restates the class's own identity** (the type list already *is* the type entries). Half of the lazy machinery already exists (`_catalogByName` is a `Lazy<>`); `BuildTypeEntries` is the leaked public construction verb.
  - `KnownTypes()`, `ComplexSchemas()`, `BuilderNames()` — same shape (methods returning collections).
- **Name-resolution thicket** (smell #3 — same logical thing exposed several times):
  - name→CLR through **three** doors: `Get`, `Clr` (`=> Get`), `ResolveType`.
  - CLR→name through **five**: `GetTypeName`, `Name` (`=> GetTypeName`), `ResolveName`, `GetTypeNameStatic`, `GetPrimitiveName`.
- **Redundant `Get`-twins** — a noun and its `Get`-prefixed double side by side: `ValidValues`/`GetValidValues`, `BuilderNames`/`GetBuilderTypeNames`.

**The OBP-clean target** (sharpened by Ingi 2026-05-31 — the first cut below was itself subtly wrong, see note):

- **The list owns all entries + all info, and returns itself.** Reading the entry list gives you the entries — no assembly smuggled into the getter. The construction work (walking modules/schemas) is *not* done behind a property named for the raw collection.
- **Each derived view is its own named member that owns its shaping.** The catalog is `app.type.entry.list.catalog` — a `catalog` property on the list that knows what to give for "catalog"; the old `BuildTypeEntries(modules)` module-scoped projection becomes *another* named view, **not** a parameter. A property returns exactly what its name says and does only that work — `Entries` returns entries, `Catalog` returns the catalog.
  - *Note: the first draft here said "Entries => _catalog.Value (lazy)". That's wrong — it names the property for the raw collection while doing catalog-assembly work behind it (work over modules/schemas that aren't the list's own). Name must match work; the catalog is a distinct named view, not a flavor of Entries.*
- Rebuild-on-`code.load` is a lifecycle event, not a getter argument; the registry owns its module set through `App`.
- **One** CLR→name member and **one** name→CLR member. Collapse the five/three down; drop the aliases.
- **Kill the `Get`-twins** — keep the noun (`ValidValues`, `BuilderNames`), drop the `Get`-prefixed double.

**Guardrail meanwhile:** branches touching this file must not *grow* the surface — no new registry verb, no sixth name-resolution door. (Stage 8's prompt-scoping already moves *away* from the all-types `BuildTypeEntries(null)` walk — good direction.)
