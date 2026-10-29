`on.cache` is the action behind a "cache it" clause ("cache for 5 minutes", "cache the result 1 hour"). It comes right after the action whose result it caches. A cached result is `%!data%`, as if the action had run.

Step text: `get https://example.com/rates, cache for 5 minutes, write to %rates%`
Formal: `http.request(Url="https://example.com/rates"); on.cache(Duration="5m"); variable.set(Name=%rates%, Value=%!data%)`

Step text: `read %path%, cache 1 hour sliding`
Formal: `file.read(Path=%path%); on.cache(Duration="1h", Sliding=true)` — each hit keeps the entry another hour.

Step text: `call LoadUser id=%id%, cache 10 minutes by "user-%id%"`
Formal: `goal.call(Name="LoadUser", Parameter=[{id: %id%}]); on.cache(Duration="10m", Key="user-%id%")` — one entry per user; left out, the step itself is the key.
