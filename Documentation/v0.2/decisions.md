# Design Decisions — guarded (context-never-null)

Each decision below is enforced by its `check:` via the Decision Guard pre-commit hook.
A commit that makes any check FAIL is blocked. To change a decision, update `agreed:` +
`check:` together, in a commit that references Ingi's sign-off. Do NOT quietly edit a check
to make it pass.

Checks use call-syntax (`\.Method(`) not bare words, so a comment mentioning the name is not
a false positive. Run manually: `scripts/verify-decisions.sh Documentation/v0.2/decisions.md`.

## no-ascanonical-in-dispatch
agreed: the generated dispatch hands over the Data reference as-is; no eager AsCanonical. A %var%/template resolves lazily on its own door (await Value()), so the handler decides.
check:  ! grep -rq "\.AsCanonical(" PLang.Generators/Emission/

## no-backing-set-flag-machinery
agreed: generated properties keep only a dumb backing field (CS9250 forces the field itself); NO __set flag, getter-fallback, or per-call reset logic.
check:  ! grep -rq "SetFlag\|__set\b\|_set = false" PLang.Generators/Emission/

## variable-set-stores-as-is
agreed: variable.set forwards the source Data verbatim (no AsCanonical, no .Value on the store path). A reference VALUE (%x%) resolves to the referenced Data INSTANCE at store — Get the instance, value door never opened, so the value stays lazy and renders at read. The marker is NOT stored verbatim (would go stale as !data rebinds, and self-assign would cycle on the door).
check:  ! grep -q "\.AsCanonical(" PLang/app/module/variable/set.cs

## variable-ref-binds-instance
agreed: a %ref% value in the store resolves to the referenced Data instance (Get), bound under the target name — never .Value() on the store path, never the marker stored verbatim.
check:  grep -q "reference.Peek() is global::app.variable.@this" PLang/app/variable/list/this.cs

## steps-enumerator-is-structural
agreed: the step enumerator yields every step with no execution context (no Disabled filter); execution skips via RunAsync's skipBelowIndent. No context dependency in the enumerator.
check:  ! grep -q "Disabled(Context)" PLang/app/goal/steps/this.cs

## no-steps-value-leak
agreed: steps.@this does not expose the raw backing List; the enumerator IS the structural surface (no public List<Step> Value).
check:  ! grep -q "public List<Step> Value" PLang/app/goal/steps/this.cs

## builder-detects-var-at-build
agreed: %var% detection happens ONLY at build — the builder stamps type.template="plang"; runtime trusts the flag and never re-scans content (HasVariable lives at the build stamp).
check:  grep -q "HasVariable" PLang/app/module/build/code/Default.cs

## data-params-backing-free
agreed: generated Data param props use the `field` keyword (compiler backing) — no hand-written __backing in the Data property emitter
check:  grep -q "get => field" PLang.Generators/Emission/Property/Data/this.cs
