Name — the identity whose private key to export, as the step names it (`export identity 'alice' …` → Name="alice"). Left out for the default identity.

- identity.export is the door to the identity's **private key**, and it refuses for now (ExportWaitsForCriticalActions): the step gets no key. Use it ONLY for a step that explicitly says to export (the key, the private key). A step that reads, fetches or gets an identity — `get my identity`, `get identity 'alice'`, `get the default identity` — is identity.get, **never** export. If the step does not say "export", it is not this action.
