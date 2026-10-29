Data — what to sign, whole, exactly as the step names it (`sign %payload%` signs %payload%). After another action in the same step it is %!data%. A `write to %x%` after the step keeps the signed result.
Contracts — the contracts this signature carries, only when the step names them (`with contracts ['C1','C2']`). Left out otherwise (the default "C0" applies).
Expires — a TTL, only when the step gives one (`expires in 5 minutes` → a duration, `PT5M`). Left out otherwise.

- signing.sign makes a verifiable signature. Taking a one-way digest — `hash %x%` — is the crypto module, not this; sign is not hash.
