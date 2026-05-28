## 2026-05-28 — Plan written for `file` → `path` module rename

The `file` module has become a misnomer. Its actions (`read`, `save`, `list`, `exists`, `copy`, `move`, `delete`) operate on `path.@this` instances, which already cover local disk and HTTP through scheme variants and will cover S3/FTP. The module name hides this — a new developer sees "file" in the catalog and assumes disk-only. The rename to `path` signals the protocol-agnostic scope at the place developers first meet the abstraction.

Conversation with Ingi settled scope: rename only. HTTP-specific configuration (headers, auth), `http.upload`/`http.download` collapse into `path.copy`, response metadata on `Data.Properties`, and the POST-vs-PUT default for `path.save` to HTTP are all deferred. They're real questions but no real-world pain has surfaced yet; designing them in advance is guessing. The `http` module stays untouched. The `app.types.path.file` namespace (FilePath, the scheme variant of the type) also stays — it's the type, not the module.

Recon counted impact: 7 C# handler files, 1 concrete consumer (`GoalCall.cs:123`), 1 stale comment (`modules/code/this.cs:251`), 11 markdown teaching files, 44 `.goal`/`.md` sources literally referencing `file.<action>`. Module-name discovery is namespace-driven (source generator reads `namespace app.modules.<name>`), so the catalog rename rides through automatically.

Stage status:
| Stage | File | Status |
|-------|------|--------|
| 1 | [Rename](stage-1-rename.md) | pending |
