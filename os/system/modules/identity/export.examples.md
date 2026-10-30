Step text: `export identity 'alice' private key`
Formal: `identity.export(Name="alice")` — refused (ExportWaitsForCriticalActions): no key is handed out.

Step text: `export the default identity's private key`
Formal: `identity.export()` — the default identity, no Name; refused the same.
