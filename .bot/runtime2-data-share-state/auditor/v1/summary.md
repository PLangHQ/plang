# auditor v1 summary — runtime2-data-share-state

## What this is

Branch lands architect/v1's "every plang variable IS Data, cross-type
views are LIVE windows" model — the core OBP-aligned Data identity
preservation work. Phase 1+2 (events→Lists, identity-preserving As<T> +
AsCanonical), Phase 3 (Variables.Set becomes dumb storage, Remove fires
OnDelete), Phase 4 (variable.set is sole binding-mint site with
MintTyped + CarryStateFromSource), Phase 5a (foreach + brought-back
.test.goals). Coder v2 closes a follow-up bug discovered while running
the LLM builder smoke (nested %var% walk on plain Data + JsonNode
dispatch in TypeConverter).

## What was done (this auditor session)

Read all prior reviews — architect/v1 plan, codeanalyzer v1/v2/v3,
tester v1/v2 — plus coder v1/v2 summaries and the report.json session
log. Verified suite ground state independently: C# 2530/2539 (9 honest
deferred-stub failures), plang 166/166. Diffed the production code vs
the merge base from `runtime2-generator-obp` (8 production files, +416
lines net of the merge). Spot-checked the As<T> four-rule contract,
AsCanonical symmetry with the typed walk, cycle protection, dumb-storage
Variables.Set, MintTyped if-chain, JsonArray + JsonNode dispatch,
list.add snapshot path. All clean within their own scope.

Then ran the cross-cutting trace none of the prior reviewers ran: who
depends on the OnChange/OnCreate/OnDelete subscriber-survival contract
that this branch changed? Grepped — only one consumer:
`App/Debug/this.cs:141-160`'s placeholder-subscribe pattern for
`--debug={"variables":[...]}`. Traced through Variables.Set replacement
+ variable.set CarryStateFromSource. Result: subscribers fire on the
first replacement, then are permanently lost. Verified empirically with
a focused TUnit test mirroring the placeholder pattern (test deleted
after verification — adding a real one is the coder's job).

Files written:
- `.bot/runtime2-data-share-state/auditor/v1/plan.md`
- `.bot/runtime2-data-share-state/auditor/v1/result.md`
- `.bot/runtime2-data-share-state/auditor/v1/verdict.json`
- `.bot/runtime2-data-share-state/auditor-report.json`

## Verdict

**FAIL** — 1 major (Debug seam regression), 1 minor (inherited
unpinned aliasing test from tester/v2), 2 nits (inherited cosmetic
carryovers).

## Code example — the trace that breaks

The placeholder pattern in Debug:

```csharp
// PLang/App/Debug/this.cs:145-159
var placeholder = Data.@this.Uninitialized(v.Name);
placeholder.OnChange.Add((oldData, newData) => LogMutation(...));
vars.Set(placeholder);
```

The replacement path in Variables.Set (current dumb-storage form):

```csharp
// PLang/App/Variables/this.cs:76-86
if (_variables.TryGetValue(name, out var prev) && !ReferenceEquals(prev, dv))
{
    prev.FireOnChange(dv);   // fires placeholder.OnChange ONCE
}
_variables[name] = dv;       // dv has empty OnChange (CarryStateFromSource
                             // cloned from a parameter Data with empty events)
```

Before commit `46b327c5`, the line after `prev.FireOnChange(dv)` was
`dv.CopyEventsFrom(prev)` — that propagated the placeholder's
subscribers onto every subsequent binding. Architect/v1 plan §Phase 3
line 290 raised the contract question; Ingi flipped to dumb storage.
The flip changed observable behavior. Debug/this.cs:141-160 was not
updated. Result:

```csharp
// User runs: plang --debug={"variables":[{"name":"x","event":"OnChange"}]}
// Goal: set %x% = 1; set %x% = 2; set %x% = 3.
// Expected: 3 OnChange logs.
// Actual: 1 OnChange log. Subsequent sets silent.
```

## Suggested next step

**FAIL → coder.** Pick one of the F1 fix paths (subscriber-restore on
dv, or Variables-collection-level subscription API) — the choice is an
architectural call for Ingi. Update Debug + add the test. F2/F3/F4 can
ride along.
