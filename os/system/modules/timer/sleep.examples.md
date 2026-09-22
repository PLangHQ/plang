`timer.sleep` pauses the goal for a while. Any phrasing asking to wait, pause, hold or sleep for a duration is this action — it is about time passing, not about a deadline on another action (that is `timeout.after`).

Step text: `wait for 60 ms`
Properties: `{"Ms": 60}`

Step text: `sleep 2 seconds`
Properties: `{"Ms": 2000}` — the step says seconds, the property asks for milliseconds.

Step text: `pause for half a second before retrying`
Properties: `{"Ms": 500}`
