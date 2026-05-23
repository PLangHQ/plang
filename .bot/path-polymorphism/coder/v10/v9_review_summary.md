# security v2 review of v9 — summary

S1 / S2-partial / S3 closed cleanly. One new Low (S4) on the consent
prompt the v9 mitigation depends on.

| ID | Class | Surface | Tester's ask |
|---|---|---|---|
| S4.a | Low | `Absolute` uses `_uri.Host` (Unicode IDN) | Render via `_uri.IdnHost` (punycode) |
| S4.b | Low | `Absolute` silently strips `_uri.UserInfo` while `_uri` keeps it on the wire | Strip UserInfo at construction OR include it in Absolute |

The security bot also flagged (non-finding) that the redirect's
`HttpContent` reuse across hops is a reliability bug for 307/308 POSTs
because HttpContent is single-send. Fixing in this round — it's adjacent
to the consent work and was their good red-team find.

## What v10 does

- **S4.a**: `Absolute` reads `_uri.IdnHost.ToLowerInvariant()` instead of
  `_uri.Host`. Same canonical form lands in the persisted grant key, so
  cache hits use punycode and a homograph variant doesn't silently match.
- **S4.b**: HttpPath ctor rebuilds `_uri` without UserInfo via `UriBuilder
  { UserName="", Password="" }`. Absolute, the URL fetched on the wire,
  and the grant key all collapse to the same userinfo-free string.
- **307/308 body preservation**: `FollowRedirect` re-buffers the request
  body into a fresh `ByteArrayContent` per hop (preserving the original
  Content headers like Content-Type), so the next hop sends the real body
  instead of an empty one against a disposed stream.

F1/F2/F4 carry-forwards remain `filesystem-permission` work.
