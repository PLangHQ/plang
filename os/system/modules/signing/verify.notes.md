Data — the signed data to verify, exactly as the step names it (`verify %signed%` verifies %signed%). Returns a bool; a `write to %x%` keeps it.
Contracts — the contracts the signature must carry, only when the step names them (`with contracts ['C1']`). Left out otherwise.
Header — expected headers to match against the signed headers, only when the step gives them (a dict).

- signing.verify checks a signature (its contracts and headers). Checking a value against a digest — `verify %content% against %hash%` — is the crypto module's verify, not this. This module is only about signed data and its contracts.
