`on.cache` is the action behind a "cache it" clause ("cache for 5 minutes", "cache the result 1 hour"). It comes right after the action whose result it caches. A cached result is `%!data%`, as if the action had run.

Step text: `get https://example.com/rates, cache for 5 minutes, write to %rates%`
Properties: `{"Duration": "PT5M"}`

Step text: `read %path%, cache 1 hour sliding`
Properties: `{"Duration": "PT1H", "Sliding": true}` — each hit keeps the entry another hour.

Step text: `call LoadUser id=%id%, cache 10 minutes by "user-%id%"`
Properties: `{"Duration": "PT10M", "Key": "user-%id%"}` — one entry per user; left out, the step itself is the key.
