`error.handle` is the modifier behind an `on error …` clause (in any wording: "on error", "if it fails", "catch error", "on failure"). It wraps the action before it; the clause's own content — a call, a set, a retry, ignore — compiles per its own module. A step with an `on error` clause uses the module doing the work AND `error`.

Step text: `read file.txt, on error call HandleMissing`
Mapping: `file.read | error.handle { goal.call }` — `file` for the work, `error` for the clause, `goal` for the recovery body.

Step text: `verify %data% with contracts ['C1'], on error call HandleContractError`
Mapping: `signing.verify | error.handle { goal.call }`

Step text: `call Process, on error set %failed% = true`
Mapping: `goal.call | error.handle { variable.set }`

Step text: `http get %url%, on error retry 3 times`
Mapping: `http.request | error.handle` — retry is a parameter of `error.handle`, no recovery body.

Step text: `write out "hi", on error ignore`
Mapping: `output.write | error.handle` — `IgnoreError` is a parameter of `error.handle`.

Step text: `throw error "retry then goal", on error retry 1 times, then call Handler`
Mapping: `error.throw | error.handle { goal.call }` — both the throw and the handler are `error`; the handler's body is `goal`.
