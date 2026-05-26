# Security v1 — purge-systemio-from-actions

## Scope

Blue/red audit of the System.IO purge. The branch's stated goal IS a
security hardening — removing ungated disk access from action handlers in
favour of a typed `path.@this` surface that auth-gates every IO touch.
My job is to verify the hardening is real and search for new attack
surface introduced by the typing flip.

## Attack model recap

PLang is user-sovereign. The trust boundary is signed `.pr`/Data.
Inside the boundary, the *user's* sovereign right is to read/write their
disk via `%!fileSystem%` etc. — that's not an attack.

What this branch is hardening is the **handler-level** discipline: an
action handler running under an actor should not bypass the actor's
permission grant via raw System.IO. Previously every handler that
reached `File.ReadAllText(absPath)` ran *under* the AuthGate. This
branch wires every disk touch through `path.@this` verbs that AuthGate-
prompt the actor.

So the threat model for *this* audit is:

1. **AuthGate bypass via new verb surface.** A new derivation verb that
   builds a `path` but skips Resolve, scheme registry, or Authorize.
2. **Wire-deserialization smuggling.** The new `PathJsonConverter`
   reads a `string` off the wire into a `path?`. If that path is then
   trusted by `Authorize`, an attacker controlling the wire can produce
   a `path` that points outside root with valid-looking shape.
3. **Execute verb scope.** New `Execute` permission gates
   `Assembly.LoadFrom`. If any Assembly-loading site doesn't use the
   `Execute` verb, the gate is paper.
4. **Goal.Path / PrPath flipped to `path?`.** A `.pr` arriving with a
   malicious typed Path is the new attack surface; previously the path
   was a `string` only sliced relative to root.
5. **Implicit `string → path` operator.** N3 from codeanalyzer flagged
   this as a footgun. I need to grade it security-wise: where does it
   fire under attacker influence?
6. **PLNG002 exemption list.** What namespaces are exempt? Could an
   attacker land their code in an exempt namespace via the unsigned-
   library-load known-accepted risk?
7. **`AsBooleanAsync` truthiness probe** — Now a `path` answering "do I
   exist?" runs through stat/HEAD. Does the probe path itself gate?
   Could an attacker use truthiness checks as a side-channel to probe
   arbitrary file existence?

## Method

Pass 1 (Blue) — read the new derivation/operations verbs, JsonConverter,
Authorize plumbing, Execute verb wiring.
Pass 2 (Red) — for each suspected vector, name the attack chain
explicitly, rate it against the threat model, propose mitigation.
Pass 3 — record verdict + open findings against the existing memory of
standing findings.

## Files to read closely

- `PLang/app/types/path/this.cs` (the operator + scheme dispatch)
- `PLang/app/types/path/this.Authorize.cs`
- `PLang/app/types/path/this.Derivation.cs` (new)
- `PLang/app/types/path/this.Operations.cs` (new)
- `PLang/app/types/path/this.JsonConverter.cs` (new)
- `PLang/app/types/path/file/this.Derivation.cs` (new)
- `PLang/app/types/path/file/this.Operations.cs` (where AuthGate lives)
- `PLang/app/types/path/permission/verb/Execute.cs` (new)
- `PLang/app/types/path/permission/verb/this.cs`
- `PLang/app/modules/code/load.cs` / `module/add.cs` /
  `code/this.Snapshot.cs` (Execute verb call sites)
- `PLang/app/goals/goal/this.cs` / `goals/this.cs` (Path-typed goal)
- `PLang.Generators/Diagnostics/Plng002.cs` (exemption list)
- `PLang/app/channels/serializers/serializer/Json.cs` /
  `serializer/plang/this.cs` (wire path)

## What I will NOT do

- Re-validate System.IO purge completeness — codeanalyzer's verified
  it and PLNG002 at error severity locks it in mechanically.
- Re-validate handler-level test mutations — tester v2 round 2 verified
  these end-to-end.
- Style/duplication issues — that's codeanalyzer's territory.
