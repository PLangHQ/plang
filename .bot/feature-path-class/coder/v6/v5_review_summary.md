# v5 Review Summary (Auditor v1)

10 findings. 1 critical, 3 major, 4 minor, 2 nit.

1. **Critical — No exception handling** in any behavior method. IOException/UnauthorizedAccessException propagate unhandled.
2. **Major — Relative property prefix bug**. `StartsWith(RootDirectory)` without trailing separator matches `/app` against `/application`.
3. **Major — Move.Overwrite silently ignored for directories**. `Directory.Move()` doesn't accept overwrite.
4. **Major — Delete non-empty dir throws** IOException instead of returning Data error.
5. Minor — Equals uses OrdinalIgnoreCase (wrong on Linux).
6. Minor — `==` operator not overridden.
7. Minor — No null guard on constructor.
8. Minor — Copy file-to-existing-directory not handled.
9. Nit — Test namespace doesn't match file location.
10. Nit — List tests rely on generator defaults for Pattern.
