Data — the value to hash; a text hashes its UTF-8 bytes · say: the value, inline · builder: as the step writes it
Algorithm — the hash algorithm · say: `with <name>` (`with sha256`, `with keccak256`) · ask: which hash algorithm does the step name? · builder: `hash X with sha256` → Algorithm="sha256", even when the value is a long interpolated string; absent only when the step names no algorithm (default keccak256)

- A dict or list hashes its json text in its own key order, so the same entries in another order give another digest.
