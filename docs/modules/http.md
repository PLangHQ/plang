# HTTP Module

Send HTTP requests, download and upload files, and stream responses. Supports request signing, configurable defaults, and streaming in multiple formats.

> **Shorthand:** if you only need to GET a URL and grab the body, the [File module](file.md#paths-can-be-urls) accepts URLs directly — `- read 'https://...' into %x%` is the same as `- get 'https://...', write to %x%`. Use this module when you need method/header/body control, signing, streaming, or want a typed response object.

## Actions

### request

Send an HTTP request.

```plang
- get 'https://api.example.com/data', write to %result%
- post 'https://api.example.com/items' with body %item%, write to %response%
- get '/api/users', write to %users%
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Url | string | yes | — | URL (absolute, relative to BaseUrl, or bare domain) |
| Method | string | no | GET | HTTP method (GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS) |
| Body | object | no | — | Request body. Strings sent as-is, objects JSON-serialized |
| Headers | dictionary | no | — | Per-request headers (merged with defaults) |
| ContentType | string | no | application/json | Request content type |
| Encoding | string | no | utf-8 | Character encoding |
| TimeoutInSec | int | no | 30 | Request timeout in seconds |
| Unsigned | bool | no | false | Skip request signing |
| OnStream | goal | no | — | Goal to call for each streamed chunk |
| StreamAs | string | no | — | Stream format: Line, SSE, or Bytes |

**Returns:** Parsed response body as the appropriate type (object for JSON, string for text, etc.).

### download

Download a file.

```plang
- download 'https://example.com/file.zip', save to 'downloads/file.zip'
- download %url%, save to %path%, if exists overwrite, on progress call ShowProgress
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Url | string | yes | — | URL to download from |
| SaveTo | string | yes | — | Local file path to save to |
| IfExists | string | no | Error | What to do if file exists: Error, Overwrite, or Skip |
| Headers | dictionary | no | — | Per-request headers |
| TimeoutInSec | int | no | 30 | Request timeout in seconds |
| Unsigned | bool | no | false | Skip request signing |
| OnProgress | goal | no | — | Goal called with transfer progress updates |

**Returns:** The saved file path.

### upload

Upload content to a URL.

```plang
- upload 'report.pdf' to 'https://api.example.com/files'
- upload %formData% to %url%, write to %response%
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Url | string | yes | — | URL to upload to |
| Content | object | yes | — | Content to upload (see content detection below) |
| Method | string | no | POST | HTTP method |
| Headers | dictionary | no | — | Per-request headers |
| Encoding | string | no | utf-8 | Character encoding |
| TimeoutInSec | int | no | 30 | Request timeout in seconds |
| Unsigned | bool | no | false | Skip request signing |
| As | string | no | — | Force content format: File, Base64, Form, or Text |
| OnProgress | goal | no | — | Goal called with transfer progress updates |

**Content auto-detection** (when `As` is not set):

| Content type | Detection | Behavior |
|-------------|-----------|----------|
| File path | String starting with `@` or file path | Reads file, sends as multipart/form-data |
| Form data | Dictionary | Sends as multipart/form-data key/value pairs |
| Text | Default for strings | Sends as StringContent |

### configure

Set default HTTP configuration for the current scope.

```plang
- configure http, base url 'https://api.example.com'
- configure http, timeout 60 seconds, unsigned
- configure http as default, base url 'https://api.example.com'
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| BaseUrl | string | no | — | Base URL for relative request URLs |
| TimeoutInSec | int | no | — | Default timeout in seconds |
| DefaultHeaders | dictionary | no | — | Default headers merged into every request |
| ContentType | string | no | — | Default content type |
| Encoding | string | no | — | Default encoding |
| Unsigned | bool | no | — | Disable signing by default |
| FollowRedirects | bool | no | — | Whether to follow HTTP redirects |
| MaxRedirects | int | no | — | Maximum redirects to follow |
| Default | bool | no | false | Apply to all requests app-wide (not just current scope) |

Only non-null values are written. Existing settings for omitted parameters are preserved.

## URL Resolution

| URL form | Example | Behavior |
|----------|---------|----------|
| Absolute | `https://api.example.com/data` | Used as-is |
| Relative | `/api/data` | Prepended with `BaseUrl` from configuration |
| Bare domain | `api.example.com/data` | Gets `https://` prefix |

## Streaming

Use `OnStream` with `StreamAs` to process responses as they arrive:

```plang
- get 'https://api.example.com/stream', on stream call HandleChunk, stream as Line
```

| StreamAs | Format | Use case |
|----------|--------|----------|
| `Line` | Newline-delimited (NDJSON) | OpenAI-style streaming APIs |
| `SSE` | Server-Sent Events | EventSource / SSE endpoints |
| `Bytes` | Raw byte chunks | Binary streaming |

The `OnStream` goal receives a `%chunk%` variable with each piece of data.

## Request Signing

By default, all requests are signed with the default identity (Ed25519). Set `Unsigned` to `true` to disable signing for a request or globally via `configure`.

## Examples

### Simple GET

```plang
Start
- get 'https://jsonplaceholder.typicode.com/posts/1', write to %post%
- write out 'Title: %post.title%'
```

### POST with Body

```plang
Start
- set %item% = { "name": "Widget", "price": 9.99 }
- post 'https://api.example.com/items' with body %item%, write to %created%
- write out 'Created: %created.id%'
```

### Configured Base URL

```plang
Start
- configure http, base url 'https://api.example.com', timeout 60 seconds
- get '/users', write to %users%
- get '/posts', write to %posts%
```

### Download with Progress

```plang
Start
- download 'https://example.com/large-file.zip', save to 'downloads/file.zip', if exists overwrite, on progress call ShowProgress

ShowProgress
- write out 'Downloaded: %progress.Percentage%%'
```
