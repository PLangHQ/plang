# Stage 1 — Output consolidation is under-specified (decision for architect)

**From:** coder. **Status:** Stage 1a landed (builder blocker fixed) + Read validated
(full goal-graph round-trip DoD) — all green, pushed. Sizing the `item`/`ICreate` drop, I
hit a real correctness fork the plan glosses. Want your call before the drop, because
getting it wrong = broken `.pr` round-trips.

## Where Stage 1 stands
- **1a ✅** `clr(json)` lowers to a host through the kind (`clr.Clr` → `Kind.Clr`; json
  bridges element→reader; `*`-kind `Read` reflects `[Store]` props off the reader). Pin
  test green, zero regressions.
- **Read validated ✅** — `GoalGraphRoundTripTests` drives a real `.pr` shape through the
  reflection `Read` and reproduces the whole goal→steps→actions→params graph (enum-as-int,
  path, nested collections). Read is proven before the STJ readers are touched.
- **Drop sized** — dropping `item.@this, ICreate` from `action` alone = **10 compile
  errors** (8 × `Data<action>` generic-constraint sites → `Data<clr<action>>`; 2 × the
  `Output` override). Contained per class; the real work is the `Data<clr<T>>` consumer
  cascade (each site's readers move `Value<goal>` → `Value<clr<goal>>`).

## The fork: "Output consolidates onto the reflection kind (verified identical)" is NOT identical

The plan (Stage 1) says hosts should stop overriding `Output` and write through the `*`
kind's `Output`, claiming it's an identical `Tagged.PropertiesFor` loop. It isn't:

```
OutputTagged  (item/this.cs:471 — TODAY's .pr writer for goal/step/action):
    writer.Name(entry.WireName)                 // "action"  — honors [JsonPropertyName], camelCase
    if (value == null) continue;                // omit nulls (WhenWritingNull)
    WriteReflected(...)                          // plang-value self-write / Data self-write / scalar

reflection.Output  (kind/behavior/reflection.cs:51 — the * kind):
    writer.Name(entry.Property.Name.ToLowerInvariant())   // "actionname"  — lowercase, ignores [JsonPropertyName]
    (no WhenWritingNull)
    type.Create(raw).Output(...)                          // different value path
```

Three divergences: **wire name**, **null handling**, **value-write path**. The wire name
is the load-bearing one: `ActionName` has `[JsonPropertyName("action")]`. `OutputTagged`
writes `"action"` (what STJ and my `Read` both expect); `reflection.Output` writes
`"actionname"`. **Delete the host `Output` overrides and route through `reflection.Output`
as-is → the `.pr` writes `"actionname"` → `Read` can't match it → the graph loses every
`[JsonPropertyName]`-renamed field.** Silent field drift — exactly the class of bug the
round-trip DoD exists to catch.

I can't just switch `reflection.Output` to `WireName`, because it's **also the writer for
foreign POCOs** (`[Out]` fields of a third-party object). That changes their output casing
from lowercase (`somefield`) to camelCase (`someField`) — possibly breaking existing
consumers/tests.

## Decision needed

**Which wire-name convention does the unified Output use?**

| | option | hosts | foreign POCOs | cost |
|---|---|---|---|---|
| **A (rec)** | unify all Output on `Tagged.Entry.WireName` | correct (`action`, camelCase) | lowercase → camelCase | verify/fix POCO-output consumers; one true path |
| B | host Output keeps a WireName path distinct from foreign-POCO Output | correct | unchanged | two Output paths — not fully "consolidated" |

**My recommendation: A.** `Property.Name.ToLowerInvariant()` in `reflection.Output` looks
like the latent bug — `Tagged.WireName`'s own comment says it "Matches STJ's
PropertyNamingPolicy.CamelCase so an Output-written shape round-trips through an STJ read."
So camelCase is the canonical wire convention; the `*` kind should have used `WireName` all
along. Unifying on `WireName` makes host write ⇄ `Read` ⇄ STJ all consistent, and the
right move for foreign POCOs is camelCase too (not lowercase). The one action item: run
the `*`-kind / POCO-output tests and confirm nothing depends on the lowercase form.

Concretely under A, "consolidate Output" = **move `OutputTagged`'s body (WireName +
WhenWritingNull + WriteReflected) onto `reflection.Output`**, then hosts drop their override
and `item.OutputTagged` is deleted — a real consolidation, not "they were already the same."

## Also confirmed (not blocking)
- Data writer emits `name, type, value` (type before value; `data/this.Output.cs:88/96/99`),
  matching the streaming reader's requirement — fresh `.pr` are Read-compatible; value-first
  `.pr` in `Tests/.build` (e.g. `markbig.pr`) are stale artifacts.
- Bridge-item audit already resolved (GoalCall value, snapshot deferred, app host,
  catalog/view obsolete).

## Ask
1. Output convention — **A** (unify on WireName) or **B** (keep two paths)?
2. Any constraint on the `Data<clr<goal>>` consumer cascade you want me to honor as I drop
   `item`/`ICreate` from the four classes?
