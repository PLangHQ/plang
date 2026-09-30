# handoff — app-systems coder

386 landed: a Data read in plang is born holding the signature it arrived in (`Data.Signature`, read as
`%x!signature.identity%`); `PendingVerification` is gone. http reads a plang response in its transport view with
the one verify `Decode` owns; `!ServiceIdentity` and `TryExtractSignedErrorIdentity` are gone; each NDJSON line's
read is that chunk's answer to the callback.

## Next — waiting on Ingi (decision 387)

The http response reshape: `ReadErrorResponseAsync` / `BuildProperties` / `ReadLimitedBytes/StringAsync` are stray
helpers over the raw `HttpResponseMessage` (verb+noun, raw hand-off of the facts at 5 exits, a `.Clr` string round
trip, `Status` + `StatusCode` stored twice). Proposed: `app/module/http/response/this.cs` — `Body(cap)` and one
`Answer(cap)`; the architect adds that the facts can be the response's own members read through the `!` hop
instead of a Properties bag. Build after Ingi's yes.

## Then

375 (2) the `!` rule with `.setting`; (3) the current-node rule; (4) the event node (shape first); (5) the
registrations sweep. 380 (4) `system.on.create` (shape first); 380 (5) read-as-text verbatim.
