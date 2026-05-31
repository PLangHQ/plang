# Coder learnings — type-kind-strict v8 (from codeanalyzer v1)

- **"Green suite" ≠ "covered."** Two `.test.goal` files named for strict-mismatch
  enforcement passed while asserting *nothing* — a PNG was accepted as a strict
  GIF. A test whose only signal is "didn't throw" cannot guard a validation
  feature. When I land a feature, the test must assert the feature *fires*, not
  merely that the happy path runs. (codeanalyzer F1)

- **Validation belongs at the seam where the data first exists, on the value —
  not at the call site against a value that isn't loaded yet.** Strict kind for a
  lazy reference fundamental had to ride *with* the value (an
  `IStrictKindEnforcer`, sibling to `IBooleanResolvable`) so it self-validates at
  byte-materialization. Pushing the check to `variable.set` against a path-backed
  image with no bytes is the consumer-owns-discipline smell.

- **A probe built from `byte[]` only is blind to the real value shapes.**
  `TryInstantiateValidator` + `ValidateKind(byte[])` silently no-op'd for a string
  path and an `image.@this` instance (neither matches an image ctor). Always test
  the validator against the shapes the binding-mint actually holds, not just the
  one that's easy to construct.

- **Folding a field means removing its wire emission too.** `Data.Kind` folded
  into `type.Kind` but kept `[JsonPropertyName("kind")] [Out, Store]`, so kind
  serialized twice (OBP smell #6). After a fold, grep the old field's
  serialization attributes. (F2)

- **Owner-discipline enforced by a string name-check is smell #5.** Gating "text
  derives no spelling-kind" with `!= "text"` in `set.cs` while `text.Build` still
  registered the hook put the rule in the consumer. The fix was to delete the
  hook (text has no spelling-kind by nature), which removed the gate entirely. (F4)

- **`Context?.` everywhere or nowhere.** One accessor (`Scheme`) dropped the `?.`
  every sibling used — a latent NRE for a Context-less entity off the wire. (F3)

- **PLang strict-mismatch on a lazy handle can't be asserted in a `.goal` yet** —
  no action triggers `image.BytesAsync`, so the throw-at-load is only reachable
  from C#. A read-lifted image carried through a variable comes back
  *path-backed* (it has a `Path`), so it defers like any path handle. Cover the
  throw in C#; let the PLang goal assert the honest lazy contract (set is clean).

- **Build failures are often LLM non-determinism, not code.** A 4-step goal
  (`read … into` + `set` + 2 asserts) failed to build 3× on step-count mismatch;
  long comment prose ("phrases that look like instructions") aggravates it. Keep
  test-goal comments terse, and don't read a build failure as a code bug without
  checking the builder error.
