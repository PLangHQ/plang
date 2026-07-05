# Coder push-back on `architect/plan.md`

**Branch:** `cli-app-property-override`. **From:** coder, after reading the plan and
verifying its claims against source (`Executor.cs:29–108`, `catalog/Conversion.cs:87–100`,
the `IsEnabled` / `Apply` / foreign-sniff sites).

**Bottom line:** diagnosis is correct, shape is right, conventions honored. I'm not
disputing the model — nullable born-with-context subsystems, access-level exposure,
convert-not-lower at the leaf, `Populate` static removed. All sound. My push-back is
about **what ships in this branch vs. what the plan lets grow unbounded**, plus one
real lifecycle gap the plan doesn't close.

Verified accurate before writing any of this:
- Crash is `Conversion.cs:97` — `Create(value, ctx).Clr(propType)`, a `text→path` **lower**
  where a convert is needed. One-line root cause, confirmed.
- Four-way branch `Executor.cs:56–100`; `IsEnabled` on Build/Debug/Tester; foreign sniffs
  at `path/file/this.Operations.cs:63,105` and `OpenAi.cs:157`; `Debug.Apply`+`_applied`;
  `Tester.Apply`; `Builder` not yet `Build`. All present as described.

---

## 1. Runtime-toggle needs a teardown contract — the plan opens a leak (§2 + §6.B)

The plan sells two things that conflict without a third:

- §2: `app.Debug = new Debug(ctx)` toggles debug **on** mid-run; `= null` toggles it **off**.
- §6.B: **all** activation (subscribe watchers, hook `OnBeforeRequest`/`OnAfterResponse`,
  compile grep, wire channel sink) moves into `new Debug(ctx)`; `_applied` guard deleted
  because "born once."

"Born once" and "rebindable at runtime" are contradictory. If the ctor subscribes and
`= null` just drops the reference, the subscriptions on the LLM hook points and watcher
lists **survive the null** (the event source still holds the delegate). Toggle off→on and
you double-fire; toggle repeatedly and you leak. `_applied` was accidentally masking this
by making re-activation a no-op.

**Ask:** pick one.
- (a) Startup-only activation — drop the "runtime toggle" selling point from §2, born once
  is honest, no teardown needed. Simplest; I lean here unless runtime toggle is a real
  requirement.
- (b) Keep runtime toggle — then `Debug` owns an `IDisposable`/teardown that
  `app.Debug` setter calls on the outgoing instance before rebinding. That's a setter with
  behavior on the app root, which is fine, but it must be in the plan, not discovered in code.

Right now the plan implies (a)'s mechanism (ctor-born, no guard) while claiming (b)'s
capability. That's the gap.

---

## 2. Bound the run-state sweep — it's unbounded discovery on a bug branch (§3)

§3 is honest that it's "a real chunk of the branch's work," and the table already flags
`Code`/`Statics` as "not yet descended — sweep them too." That's the problem: "audit every
public setter reachable through the walk, tree-wide" has no fixed edge. It grows every time
someone finds another run-state setter, and each demotion to `internal set` is a potential
break in whatever runtime code was using the public setter.

**Ask:** scope the sweep to nodes the walk descends for a **real flag that exists today** —
Build, Debug, Tester, CallStack (the §3 leaf table minus the "sweep them too" open items).
File `Code`/`Statics` and any other node with no live flag as a follow-up todo. Rationale:
a public setter nobody's walking into isn't a CLI-exposure bug yet — it becomes one when a
flag reaches it. Demoting them now is speculative and widens the diff without fixing the
crash or any live over-exposure. This keeps the branch's blast radius auditable.

Not disputing the principle (public-set = config surface). Disputing doing the *whole tree*
in the branch whose job is "fix the startup crash + establish the model."

---

## 3. Stage the entry-action dissolve — don't do it inline (§6.C)

§6.C dissolves three mode-inspections at once: `if (Builder.IsEnabled) return RunAsync()`
(`this.cs:545`), Start-routing (`:610`, `Executor.cs:104`), **and** the in-memory-store
selection (`Tester.IsEnabled → Sqlite.InMemory`). That's the run root plus datasource
selection in one move — highest blast radius in the branch, and a regression there is hard
to localize because it spans entry dispatch and persistence.

The plan already sanctions the fallback: "one owned `if (Build != null)` at the run root is
the single acceptable inspection." **I want to take that fallback for this branch** and land
the dissolve as its own follow-up. Sequence:

1. Presence replaces `IsEnabled` everywhere (the rename half of §6.C is safe + mechanical).
2. One owned `if (Build != null)` / `if (Tester != null)` at the run root and the store
   selection — green, localizable.
3. Dissolve to entry-action set-at-birth in a separate branch, verified against Start/Build/
   test routing on its own.

This isn't disagreement with "dissolve is the target" — it's refusing to couple the target's
risk to the crash fix.

---

## 4. Watch the reusable navigator for YAGNI (§1 / commit 36db790c0)

"Factor the walk callable independent of argv so a future `set %!cache.name%` plang door can
call it" — the plang door doesn't exist and §8 itself preaches "build recursion when a real
nested property exists." The navigator is justified **only if it costs nothing over an inline
walk**. If making it argv-independent adds an interface/indirection layer purely for the
hypothetical door, that's the YAGNI §8 warns against.

**Position:** I'll write the walk to do the CLI job cleanly. If the resulting shape is
naturally callable from elsewhere, good — reuse falls out. I won't add abstraction *for* the
future door. Flagging so we don't relitigate in review when the walk isn't a standalone
`IAppTreeNavigator`.

---

## 5. Accept + flag (no change asked)

- **§9 D-interim:** agree — mechanical `!= null` + visible TODO over an `App.Building`
  accessor. The accessor reads like `IsEnabled` resurrected and hides debt. Noting for the
  record: this knowingly leaves 2 scattered `!= null` sniffs (file + llm layers), violating
  the "no scattered `!= null`" rule *on purpose*, deferred to the inversion branch. Fine as
  long as it's a real deferral with the TODO markers, not a forget.
- **Callstack UX change (§1):** `--debug` stops setting `CallStack.Flags`; users bundle it as
  `--callstack={"flags":...}` now. Correct by the tree-mirror principle, but it's a visible
  behavior change — needs a changelog/release note line so it doesn't read as a regression.
- **`context.Diagnostic` (§6.A):** agree with moving the sink to the Debug channel; matches the
  Console-ban rule (diagnostics → Debug channel, gated by the channel, not a `?.` per caller).
  Name is yours to settle; mechanism is right.

---

## Path I intend to take (revised from plan §Path)

1. `app.Builder → app.Build` rename; drop `--builder` alias + normalization.
2. The convert walk in `Configure` (fixes the crash — `TryConvert` at the leaf, not `.Clr`);
   delete the four-way branch + `catalog.Populate`.
3. Subsystems nullable, `new T(ctx)`, `_app → Context.App`; delete `IsEnabled`. **Decide §1
   (startup-only vs. rebindable teardown) before writing the ctor activation.**
4. **Bounded** run-state sweep: Build/Debug/Tester/CallStack leaf table only; `Code`/`Statics`
   → follow-up todo. Give `Tester.Include`/`Exclude` a settable `List<string>`.
5. Validation onto types (`uint` timeout/parallel, enum/`choice` format); delete `Tester.Apply`.
6. `Debug.Write → context.Diagnostic`; `Debug.Apply` activation → ctor; delete `_applied`,
   shorthand, callstack cross-node write.
7. **Staged** entry dispatch: one owned `if (Build != null)` / store selection at the run root;
   full dissolve deferred.
8. D sites: mechanical `!= null` + TODO.
9. Regression: `--build={"files":[...]}` builds + runs `Hello.goal`, no startup crash.

Deltas from the plan's Path: step 4 bounded (was tree-wide), step 7 staged (was dissolve).
Everything else unchanged. If the architect wants the full sweep/dissolve in-branch, say so
and I'll size it — but my default is to land the crash fix + model green first and defer the
two open-ended pieces.
