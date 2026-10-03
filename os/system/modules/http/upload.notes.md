Url — where to upload to · say: the url, inline · builder: as the step writes it
Content — the file, or form parts, to send · say: `upload '<file>' to <url>`, `upload %data% to <url>` · builder: a quoted file name is the path itself — `upload 'photo.png'` → `Content="photo.png"`, NEVER a `%!…%` (a quoted literal is never an app value); a `%variable%` stays the variable.

- Posting a json/dict body is http.request; only sending a file or form parts is this upload.
