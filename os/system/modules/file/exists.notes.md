Path — the file or folder to check for · say: `check if '<path>' exists`
Returns — the path itself; whether it exists is the value's truthiness, so `if %x% is true` probes it (a filesystem stat, or an HTTP HEAD for a URL) at the moment you test it.
