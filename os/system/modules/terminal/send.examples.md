Step text: `send %!data% to %container%`
Properties: `{"Data": "%!data%", "Process": "%container%"}` — `%container%` holds a running program (from terminal.open).

Step text: `send 'ls -la' to %shell%`
Properties: `{"Data": "ls -la", "Process": "%shell%"}`
