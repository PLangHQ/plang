# `SignedData` → `Signature` (OBP cleanup)

Mechanical refactor. Coder task. Bundle into the same PR as the implementation, or do it standalone first — coder's call.

## What

Rename `App.modules.signing.SignedData` → `Signature` throughout. The current name is a leftover from the early runtime2 rewrite before OBP shape was fully formed. The class represents *the signature*, not "signed data" — the data being signed is `Data.@this`, not the envelope.

New canonical type: `App.modules.signing.Signature`.

## Affected files (inventory, not exhaustive — coder verifies)

- `PLang/App/modules/signing/SignedData.cs` → rename file and class to `Signature.cs` / `Signature`.
- All references in `Ed25519Provider`, `sign.cs`, `verify.cs`.
- Any consumer that reads `Data.Signature` (which already exists as a property — check whether the property name needs adjusting; if it's typed `Signature`, the rename composes cleanly).

## Risk

Low. Pure rename, no semantic change.
