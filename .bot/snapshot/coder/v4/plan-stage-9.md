# snapshot — coder v4 — stage 9: signed, in its own view (58, 59, 64, 65)

## The change

1. `View.Snapshot`: a store view that leaves `[Sensitive]` members out. It shares store's code: `snapshot : store`,
   overriding only `Shows` and the sensitive leaf.
2. The sensitive leaf hook: `View.Sensitive(characters, writer)` writes the leaf (`****` by default, the characters in
   store, null in a snapshot); `IWriter.Sensitive` keeps its signature and hands the leaf to its view.
3. The signature's `view` (text: `store` | `out` | `snapshot`): written on the wire and inside the signing bytes (an
   older signature with none signs and verifies as before). Read: a recorded `snapshot` reads at rest (Origin
   Stored); `store`/`out` keep the reader's rule. Verify re-hashes in the recorded view.
4. Signing in a view from C#: `ISigning.SignAsync(sign, View)`; `SignAsync(sign)` passes the step's StoreView as now.
   The catalog doesn't change.
5. `snapshot.save`: the walked snapshot written once in View.Snapshot through the plang writer (the digest's own
   writer), answered as a wire of type snapshot over those bytes, signed from C# in View.Snapshot.
6. The snapshot's text writes in View.Snapshot.
7. `resume` verifies first: `SnapshotUnsigned`, `SnapshotSignatureInvalid` (verify's error as its cause),
   `SnapshotNotTheApps` (no identity of the app's), `SnapshotSignerArchived`.
8. FreshAppGoesOn: `run.plang`, written by a run of Record.goal (the builder rebuilds the goals).

## Tests (red first)

- ASensitiveMember green; a sensitive leaf comes back null.
- One `.plang` resumed twice; one older than the window.
- A store row and a file both verify.
- Refused: a hand edit, another identity, an archived identity, an unsigned snapshot.
- A store-signed Data through Out still meets the window and the replay check.
- A flipped `view` fails.
- Surface re-pinned citing "snapshot 64, 65".

## Risk to pin first

The hash: the save hashes the wire's raw bytes; a `.plang` read gives the snapshot back eagerly (its reader is eager), and
verify re-writes it through the plang writer in View.Snapshot. The two must be the same bytes. A test pins it before the
rest is built on it.
