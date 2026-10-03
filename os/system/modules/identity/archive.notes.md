Name — the identity to archive, as the step names it.
Force — true only when the step says `force`. Left out otherwise.

- identity.archive soft-deletes an identity (hidden from listings, keys kept). Only a step that says to archive (archive, soft-delete) is this. Restoring an archived one is identity.unarchive.
