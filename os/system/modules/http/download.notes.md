Url — the address to download from · say: the url, inline · builder: as the step writes it
Path — the file/path to write the bytes to, when the step names one (`download %url% to '/x.zip'`) · say: `to <path>`, `save to <path>` · builder: the path the step names; left out, the bytes come back in %!data%
Header — request headers, a dict · say: `headers Name: value` · builder: each the step names

- Use http.download ONLY when the step says `download`, or `save`/`write` a URL to a file or path. A `get`/`fetch`/`post <url>` that reads the answer is http.request, never this.
