# Stabilization — remaining issues (after Cluster 1)

Follow-up to `stabilization-findings.md`. Trajectory **361 → 217**, all root-cause fixes, zero
regressions:
- NRE Uninitialized-sentinel guard (−125) — landed.
- **Cluster 1 (text→choice)** — landed per your ruling + the reader follow-up (`Parse(symbol)`,
  three ICreate faces, closed `Reader<T>` per set, no read-time reflection). Condition suite
  recovered; zero text→choice failures remain.

Two clean root-cause clusters remain, plus a scattered long tail. Descriptions below; my
recommendation at the end.

## Cluster 2 — `http.request.Run` NRE, `Http` provider unbound (12) — execution-path question

**Root, traced:** `[Code] IHttp Http` is bound in the generated **`Attach(action, context)`**
(`app.Code.Get<IHttp>() → __Http_backing`). The getter is `Http => __Http_backing!`. The failing
tests reach `Run()` (`request.cs:85 await Http.SendAsync(this)`) with `Attach` never called, so
`__Http_backing` is null and the `!` masks it into a bare NRE. The provider itself IS registered
(`module/code/this.cs:258 RegisterBuiltIn<IHttp>`), so this is not an http bug — it's that the
value never got attached on the path these tests exercise.

**The question for you:** is the failing path a **test-harness gap** (the tests build+`Run` an
action without the `Attach` step the real pipeline does — my memory says tests should use the
`App.Run` real path, not call `Run()` directly), or did `Attach` stop firing for `[Code]` on the
real path (a regression)? I lean test-harness — `Attach` is action-lifecycle and the 12 are all
direct-`Run` module tests — but it needs your read before I touch either. Independent of that: the
`__Http_backing!` null-forgiving turns an unattached provider into a bare NRE; a real throw
("IHttp not attached — the action's Attach() didn't run") would self-diagnose this whole class.

## Cluster 3 — `json.Writer` rejects a bare `app.goal.@this` (5) — a raw goal reaches the wire unlifted

**Root, traced:** `writer.Value(object normalized)` (`json/writer.cs`) accepts `dict`, `list`,
`item.@this v → v.Write(this)`, `IEnumerable`, else throws. A `goal` is a **host** (`clr(goal)`
per the plan), not an `item.@this` — so a bare `app.goal.@this` handed to the writer falls to the
throw ("Normalize is missing a case").

**The connection to the defork:** my born-native lift now sends a non-item host through the clr
rung — `item.@this.Create(goal, ctx)` → `clr(goal)`, which serializes fine via the clr carrier's
reflection Output. So the failure is a **producer that stores a RAW `goal` into a Data without
lifting it** (bypassing `Create`), or a Normalize path that hands the writer the bare host. Two
candidate fixes for your ruling: (a) the producer lifts (→ `clr(goal)`) at construction — the
value model says every value is born through `Create`; or (b) `writer.Value`'s fallback routes a
non-item CLR object through the clr carrier instead of throwing (symmetric with the lift's clr
rung). I lean (a) — find the producer; the throw is correctly loud, and (b) would paper a
construction-site bug. Tests: `GoalsTests`, `DiscoverActionTests`, `GoalMimeDeserializationTests`.

## The long tail (~26) — scattered, not one root

`Expected to be equal to Q` (12), `contain Q` (7), `be true` (7) are **individual** assertion
failures across unrelated tests, not a shared signature — each is its own small issue (no cheap
batch fix). Plus known buckets, no create-model work:
- **Snapshot rebuild/restore** (2) — intentional deferral (ISnapshot redesign).
- **`List cannot lower to this Clr projection`** (2) — `goal.getTypes` keepalive; **dies at Stage 4**.
- **Deserialization-constructor bind** (2) — STJ ctor-parameter mismatch; likely the STJ-collapse
  tail (a type whose reader hasn't moved off the constructor-binding path). Worth a look but small.

## Recommendation — what I'd take next

**Cluster 3 (json.Writer-goal)** — it's in my wheelhouse (create/serializer, connects straight to
the defork), a clean 5-test cluster, and the fix is likely one producer lift once you rule (a) vs
(b). **Cluster 2 (http, 12)** is bigger but it's an execution-path/test-harness call I want your
read on first (test-harness vs Attach-regression) — cheap to recover once decided, but I don't want
to change test setup or the `!` masking without your steer. After those two + Stage 4 retiring the
List-lower cluster, the residue is the ~26 scattered assertions + the snapshot deferral — a
per-test cleanup pass, not root-cause work.

My vote: **json.Writer-goal next** (pending your (a)/(b) ruling), then http once you call
test-harness-vs-regression.
