# Auditor summary — singular-namespaces

**Latest version:** v1 — **VERDICT: PASS**

## What this is

Cross-cutting audit of a 4-stage refactor (singular namespaces + non-null invariants
+ accessor reshape + type-entity move; 804 files; merged plang-types mid-flight).
Pipeline already had codeanalyzer v1→v4 (last PASS on coder v2) and tester v1→v3
(last PASS on coder v3). No security review on branch.

## What was done in v1

Read all four bot summaries + codeanalyzer v4 report + tester v3 verdict. Diffed
`codeanalyzer-v4..HEAD` — coder v3 changed only tests + `.pr` + `Capture.goal`, so
codeanalyzer v4's production verdict still applies. Spot-traced:
- The four codeanalyzer-v4 latent findings in HEAD (F2 fixed in v3, F1/F3/F4 still present).
- Producer-stamping mechanism: `Data.Context` setter at `data/this.cs:80-81`
  propagates onto `_type.Context`. Verified covers the ~30+ `FromName(...)` call sites.
- Architectural fit: `type/this.cs` (entity) + `type/list/this.cs` (registry) matches
  architect's stage-4 plan.
- Cache-build interaction: still safe via `_foldLoaded=true` in 2-arg ctor (catalog
  entries spare the Context check, even after `Promote()` throws on unstamped reads).

Clean-rebuilt `PlangConsole` (0 errors, 254 pre-existing warnings), ran
`PLang.Tests` → **3696/3696**, ran `plang --test` from `Tests/` → 245 pass + 8
HTTP transients (`httpbin.org` flake; same shape tester reported).

## Verdict & findings

**PASS.** Prior bot verdicts hold up. Five findings, all minor/nit:
- F1 (minor): no security review on branch → recommend routing before docs.
- F2-F4 (nit): codeanalyzer v4's latent F1/F3/F4 still in HEAD. Worth a one-line
  cleanup pass if coder is invoked again; not blocking.
- F5 (minor cross-file): producer-stamping invariant is load-bearing across module
  boundaries. Defense-in-depth (Promote throw) is in place. Worth documenting in
  `good_to_know.md`.

## Code example — the cross-file invariant worth documenting

```csharp
// data/this.cs:135 — ctor sets _type directly, no Context propagation.
public @this(string name, object? value = null, type? type = null, @this? parent = null)
{
    _value = UnwrapJsonElement(value);
    _type = type;   // <-- bypass setter; _type.Context stays whatever the caller had
    ...
}

// data/this.cs:74-83 — Context setter is what wires _type.Context.
public actor.context.@this Context
{
    set
    {
        _context = value;
        if (_type != null) _type.Context = value;   // <-- the load-bearing line
        ...
    }
}

// Invariant: Engine (or any consumer) MUST call data.Context = ctx
// before any reader touches data.Type.Fields / .Values / .Example.
// Producers that violate it get a fail-loud throw from Promote (good).
```

## Next

```
run.ps1 security singular-namespaces "Security review of branch singular-namespaces" -b singular-namespaces
```

(After security PASS → docs. If security wants to skip and go straight to docs,
that's a judgment call for the dispatcher.)
