# Transparent Data signing — the IO hook

The `signing` module already has the high-level pipeline (`Ed25519Provider.SignAsync` at `PLang/App/modules/signing/providers/Ed25519Provider.cs:23`). What's missing is the *automatic invocation* on Data serialization.

## Where the hook lives

`App.Channels.Serializers` is the IO boundary for Data. The serializer's Data-specific path needs one addition before writing:

```csharp
if (data is Data.@this d && d.Signature == null)
    d.Signature = await signing.SignAsync(d, expiresInMs: null);  // default: no expiry
```

On read, the deserializer populates `d.Signature` from the serialized form as-is — *unverified*. Verification is the consumer's explicit step, not automatic on read (otherwise every Data read pays a crypto cost when most readers don't care about integrity).

## Why default no-expiry

Error callbacks may legitimately be inspected and re-run weeks or months later. Integrity ("this is the unmutated callback we issued") is the durable guarantee; validity ("still valid at time T") is opt-in via explicit `- sign expires in ...`.

## Performance

Ed25519 signing is ~50µs. For the volume of Data IO PLang does, this is in the noise. If a future workload makes it a problem, the right fix is per-channel opt-out (debug/stderr channels skip signing), not removing the default.

## Open question for the coder

Does *every* Data write get signed, or only Data writes to "external" channels (file, http, named outputs) — debug/stderr/internal channels skip?

**Architectural answer:** sign always. Implementation can add per-channel opt-out if a real perf case appears. Don't pre-optimize.
