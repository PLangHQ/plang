# Stabilization findings — the ~224 remaining reds, root-caused

Ingi asked whether to fix the branch's accumulated reds before Stage 4. The branch drifted from
Stage 0's **129** to **~361**. I fixed the dominant cascade already; this documents the rest,
root-caused, for your ruling on the create-model ones. Per Ingi: investigate-all-fix-none, then
Stage 4.

## Already fixed (landed, `30d95c69e`) — the Uninitialized-sentinel NRE cascade: −125

The null-model change made unset optional slots resolve to `data.@this<T>.Uninitialized(name)`
— a **non-null sentinel**. Consumers still guarded `action.X == null` before
`(await X.Value()).Clr<>()`, so the guard never fired: `.Value()` returned null, `.Clr` NRE'd.
Added `|| await X.IsEmpty()` (the already-correct `http Body` pattern) at 8 direct-deref sites
(llm Tools ×2, http Headers/DefaultHeaders ×3 variants, mock Parameters, build Actions ×2).
**361 → ~224, zero regressions** (by-name vs HEAD baseline). Every remaining cluster below is
**pre-existing** (in the HEAD baseline before any of my work) — accumulated Stage 1–3 debt.

## Cluster 1 — text→`choice` declines (BIGGEST create-model gap, ~13+ tests) — NEEDS YOUR RULING

**Symptom:** `[CreateItemDeclined] %Operator% holds a text — 'choice' cannot be created from it.`
Every `if`/`compare`/`elseif` fails — `%Operator%` is typed
`data.@this<choice.@this<Operator>>` (`condition/compare.cs:10`, `if.cs:12`, `elseif.cs:11`),
authored as a text (the operator name/symbol), and text→choice declines.

**Root:** `choice/this.cs` has `FromName(string, ctx)` (parses a name → the choice) but **no
`ICreate` face that uses it** — it inherits the default `Create(raw) => raw as choice<T>`
(pass-through only). So a text name can never become a choice. The create-unification dropped
whatever `Build`-hook used to parse it.

**Blast radius:** 19 `choice.@this<…>` param sites across modules (condition Operator, http
`Method`/`StreamFormat`, llm, …). The CLR-enum default works (`(HttpMethod)0 → As<choice>` via
the apex enum rung); only **text-authored** choice params fail. Fixing this one face likely
recovers the whole condition suite + more.

**Proposed fix (your call — this is create-model):** give `choice<T>` an ICreate face that
coerces a text name via the existing `FromName`:
```csharp
// choice/this.cs — the name coerces into the choice (a text "Equals"/">" binds to choice<Operator>).
public static @this<T>? Create(object? raw, global::app.actor.context.@this? ctx)
{
    if (raw is @this<T> self) return self;
    var name = (raw as global::app.type.item.@this)?.Clr<object>()?.ToString() ?? raw as string;
    return name is null ? null : FromName(name, ctx);   // FromName throws on an unknown name → wrap to decline?
}
```
Open questions for you: (a) FromName currently **throws** on an unknown name — should the Create
face swallow→decline (return null) or let it surface? (b) operators are authored as symbols
(`==`, `>`) as well as names — does `FromName`/the Operator enum cover both, or is there a symbol
table that also died? (c) is `choice` the right owner, or should this be an `IRawNameResolvable`-
style path like `variable`?

## Cluster 2 — `http.request.Run` NRE, `Http` provider null (12) — wiring, needs a decision

**Symptom:** NRE at `request.cs:85` `await Http.SendAsync(this)`. Generated getter is
`public partial IHttp Http => __Http_backing!;` — the `[Code] IHttp` backing field
(`__Http_backing`) is **never injected** in these test contexts, and the `!` masks the null.

**Root:** the `[Code]` provider `IHttp` isn't resolved/registered for the action in the test
app. Either (a) a test-infra gap — the fixtures never wire an `IHttp` (was previously wired and a
harness change dropped it), or (b) a provider-resolution regression in the `[Code]` binder. I did
not fully trace the injection path (investigate-only); flagging it as the next step. Note the `!`
null-forgiving on `__Http_backing!` turns a wiring miss into a bare NRE — a clearer failure
(throw "IHttp provider not registered") would self-diagnose this whole cluster.

## Cluster 3 — `json.Writer` rejects `app.goal.@this` (~4) — missing Normalize case

**Symptom:** `NormalizeException: json.Writer received a value of type app.goal.this that isn't
part of the tree contract. Normalize is missing a case for this type.` (`writer.cs:197`).

**Root:** `Normalize` (`data/this.Output.cs`) decomposes values into the writer's tree contract
(variable/dict/list/leaf) but has **no case for `goal`** — a goal reaches the writer un-decomposed
and it throws. Either Normalize should decompose goal via its own tagged output, or a goal should
never reach `json.Writer` on the path these tests exercise. Needs a case added (or the producer
that lets a goal leak to the wire identified).

## Cluster 4 — `List cannot lower to this Clr projection` (~2) — leave, dies at Stage 4

`InvalidCastException: List\` cannot lower to this — the type must own this Clr projection.` This
is the `goal.getTypes` keep-alive the plan already marks `[Obsolete]` and slates for deletion at
Stage 4 ("its List-lower crash is the same terminal throw… keep-alive only, no dedicated
investment"). No action.

## Cluster 5 — Snapshot rebuild/restore (~7) — known intentional deferral, stays

`Snapshot could not be rebuilt from wire JSON` / `Snapshot restore is deferred to the ISnapshot
redesign`. Already deferred by design (Stage 0 counted these); resolves with the ISnapshot
redesign. Documented, no action.

## Summary for the ruling

| Cluster | ~size | Kind | Who |
|---|---|---|---|
| 1 text→choice | 13+ | create-model gap | **architect ruling** |
| 2 http Http-null | 12 | provider wiring | coder investigate + maybe test-infra |
| 3 json.Writer goal | 4 | missing Normalize case | coder (small) |
| 4 List-lower | 2 | goal.getTypes keepalive | dies Stage 4 |
| 5 snapshot | 7 | intentional deferral | ISnapshot redesign |

Cluster 1 is the highest-leverage and is yours to rule (it's the create model). Per Ingi I'm
moving to **Stage 4 (module-discovery)** now; these findings wait for your ruling on 1–3.
