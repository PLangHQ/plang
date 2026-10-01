`on.timeout` is the action behind a "timeout after …" clause. It always bounds work the step does — `read the file, timeout after 5s`; a step that only waits for time to pass, with nothing to bound, is the timer module. The Timeout error is caught by an `on.error` clause like any other, and each retry gets a fresh deadline.

Step text: `get %url%, timeout after 10 seconds`
Properties: `{"After": "10s"}`

Step text: `call Import, timeout 2 minutes, on error call Alert`
Properties: `{"After": "2m"}` — the on.error after it catches the Timeout.
