Name — the identity to get, as the step names it (`get identity 'alice'` → Name="alice"). Left out for the default identity (`get my identity`, `get the default identity`).

- identity.get reads an identity (auto-creating a default if none exist). It **never** exports the private key — a step that says export is identity.export, not this.
- Getting ALL identities — `get identities`, `list identities` — is identity.list, not get.
