Url — the address to request · say: the url, inline · builder: as the step writes it; a bare domain gets https://
Method — GET, POST, PUT, DELETE, … · say: the verb (`get`, `post`, `put`, `delete`) · builder: from the step's verb; GET is the default when the step says none
Body — the request body, a json object or a dict · say: `body {…}` · builder: a `body {…}` (also `with {…}`, `sending {…}`) is the Body; posting a json/dict body is this request, never http.upload (which is for a file or form part)
Header — request headers, a dict · say: `headers Name: value` · builder: each `headers X: Y` the step names, into the dict

- Returns the parsed response body (json becomes navigable, text stays text).
