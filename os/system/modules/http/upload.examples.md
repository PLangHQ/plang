Step text: `upload 'photo.png' to https://api.example.com/images, write to %result%`
Formal: `http.upload(Url: text = "https://api.example.com/images", Content: text = "photo.png")` — a file (or form parts) sent as multipart; the trailing `write to %result%` is its own action.
