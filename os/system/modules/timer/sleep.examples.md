`timer.sleep` pauses the goal for a while. Any phrasing asking to wait, pause, hold or sleep for a duration is this action — it is about time passing, not about a deadline on another action (that is `timeout.after`).

Step text: `wait for 60 ms`
Mapping: `timer.sleep Ms([number] 60)`

Step text: `sleep 2 seconds`
Mapping: `timer.sleep Ms([number] 2000)`

Step text: `pause for half a second before retrying`
Mapping: `timer.sleep Ms([number] 500)`
