`timer.sleep` pauses the goal for a while. Any phrasing asking to wait, pause, hold or sleep for a duration is this action — it is about time passing, not about a deadline on another action (that is `timeout.after`).

Step text: `wait for 60 ms`
Properties: `{"Duration": "60ms"}`

Step text: `sleep 2 seconds`
Properties: `{"Duration": "2s"}` — a number and its unit.

Step text: `pause for half a second before retrying`
Properties: `{"Duration": "500ms"}`
