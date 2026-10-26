# Identity and signing

For someone deciding whether to trust PLang. The question this page answers: how does PLang know who signed something, and can anyone take a private key out?

## An identity

An identity is a name and an Ed25519 key pair. The first time a goal asks for one, a default is made:

```plang
- get identity, write to %me%
- write out 'has identity: %me.name%'
```

printed `has identity: default` in an app that had none. `test/module/identity/autocreate` passes. After that run, `.data/` held two SQLite files: `.data/data.sqlite`, new since the identity was made, and `.data/setting/data.sqlite`, the settings store. Which of them holds the identity was not checked. The whole `.data/` folder is refused to other actors (see [files and permissions](files-and-permissions.md)).

## Signing

`sign` attaches a signature to data. `verify` checks it:

```plang
- sign %message%, write to %signed%
- verify %signed%, write to %isValid%
```

printed `Valid: true`. The signature covers a hash of the data, a nonce, a timestamp, the identity and the contracts. These tests pass in `test/module/signing` (9 of 9):

- a signed value verifies (`SignAndVerifyRoundTrip`)
- data changed after signing fails (`ContentChangedAfterSigningFailsVerify`)
- a property added after signing fails (`PropertyAddedAfterSigningFailsVerify`)
- an unsigned value fails (`SigningVerifyUnsigned`)
- a different contract fails (`SigningContractMismatch`)
- an expired signature fails (`SigningExpired`)
- a signed value survives being put in a list (`SignedDataSurvivesInList`)

Further tests exist as folders (`noncereplay`, `timedout`, `tampereddata`, `withheaders`); they are not in the 9 the runner picked up, so they are not claimed here.

## Saved permission grants are signed

An "always" grant is saved signed by the actor's identity. A tampered row reads as no grants. This is the design doc's statement (`Documentation/v0.2/filesystem-permission.md`); the signature check on a stored grant was not run for this page.

## The private key does not leave

`identity.export` is the door to the private key. It refuses:

```
export: ExportWaitsForCriticalActions
```

A step `export identity 'alice', write to %exported%, on error write out …` printed that error key and went on. The refusal is by design: a private key leaves the app only through a critical action that asks more than a yes, and until those exist no key is handed out and nothing is asked (`os/system/modules/identity/export.description.md`).

This refusal covers the `export` action. It does not mean the key is unreadable: the files in `.data/` were `rw-r--r--`, (the settings' store folder opens as `rwx------`, see [keys](keys.md); the folder these files were in was not checked). Whether the private key is plain in one of them was not checked.

## Tests

`test/module/signing` (9), `test/module/identity` (1), `test/module/crypto` (5) pass. No test was found for the `identity.export` refusal.
