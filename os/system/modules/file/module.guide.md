## Paths can be URLs

The `Path` on every action in this module is polymorphic. Anything that looks like a URL is routed to the matching scheme handler; a bare path or `file://` goes to the local filesystem.

```plang
- read 'config.json', write to %config%
- read 'https://api.example.com/users.json', write to %users%
- read %source%, write to %content%
```

The last one works whether `%source%` holds a local path or a URL — the program doesn't care which. The registered schemes are `file://` (and bare paths) and `http(s)://`:

| Action | `file://` (local) | `http(s)://` |
|--------|-------------------|-------------|
| read | open and read | GET |
| save | write to disk | POST (the server decides; 405 surfaces as `MethodNotAllowed`) |
| exists | stat | HEAD (2xx means it exists) |
| delete | remove | DELETE |
| list | directory entries | server-defined, usually unsupported |
| copy / move | filesystem copy / rename | read the source, then write the destination |

**Consent applies to any path.** The first time your program touches an HTTPS URL, the runtime asks the same way it does for a local file — `Allow worker to read https://api.example.com/users.json? (y/n/a)`; answering `a` remembers the grant. Grants are scoped per actor, path, and verb, so the same URL read by two actors is asked of each. For an HTTP URL the path is canonicalized first — scheme and host lowercased, the default port (80 for http, 443 for https) dropped, query parameters sorted by key — so `HTTPS://API.example.com/u.json?b=2&a=1` and `https://api.example.com/u.json?a=1&b=2` count as the same resource.

**Errors come back as data.** A non-2xx response is not an exception — it arrives the way a permission denial or a disk-full error does, and you handle it with `on error`. The status maps to an error key: 404 is `NotFound`, 405 is `MethodNotAllowed`, and a network failure is `NetworkError`. See the [http](http.md) module for the full mapping.

**When to use which.** `read %url%` is the shorthand for "GET this and give me the body." Reach for the [http](http.md) module (`- get %url%, write to %x%`) when you need to set the method, headers, or a request body — it exposes the full verb surface; this module is the one-liner.
