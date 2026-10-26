# The guarded decisions

**Chosen.** Some design decisions are written as a name, an `agreed:` sentence and a `check:` command in `Documentation/v0.2/decisions.md`. A commit that makes a check fail is meant to be blocked by a pre-commit hook. To change a decision, update `agreed:` and `check:` together "in a commit that references Ingi's sign-off. Do NOT quietly edit a check to make it pass" (`decisions.md:3-6`).

**The list, run on this checkout.** `bash scripts/verify-decisions.sh Documentation/v0.2/decisions.md`:

| Decision | Agreed | Result |
|---|---|---|
| `no-ascanonical-in-dispatch` | the generated dispatch hands over the Data as is; a `%var%` resolves lazily on its own door | PASS |
| `no-backing-set-flag-machinery` | generated properties keep a dumb backing field; no `__set` flag | PASS |
| `variable-set-stores-as-is` | `variable.set` forwards the source Data verbatim | PASS |
| `variable-ref-binds-instance` | a `%ref%` resolves to the referenced Data instance, bound under the target name | **FAIL**: its check greps `PLang/app/variable/list/this.cs`, a path that does not exist (the variable list is at `PLang/app/type/item/variable/list/this.cs`), and the string it looks for, `reference.IsVariable`, appears in no file under `PLang/app` |
| `steps-enumerator-is-structural` | the step enumerator yields every step with no execution context | PASS |
| `no-steps-value-leak` | `steps.@this` does not expose its raw list | PASS |
| `builder-detects-var-at-build` | `%var%` is detected only at build; the runtime trusts the flag | PASS |
| `data-params-backing-free` | generated Data parameters use the `field` keyword | PASS |

**Why.** The file's header gives the mechanism, not a reason for each decision. Per-decision reasons: reason not recorded.

**Held by.** `scripts/verify-decisions.sh` and `.githooks/pre-commit`. `git config core.hooksPath` is empty on this checkout, so the hook is not wired here; the script is run by hand. The `FAIL` above is a stale check, not evidence that the decision was broken; it is reported to the docs role.
