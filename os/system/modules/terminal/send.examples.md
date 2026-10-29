Step text: `send %!data% to %container%`
Formal: `terminal.send(Data: item = %!data%, Process: process = %container%)` — `%container%` holds a running program (from terminal.open).

Step text: `send 'ls -la' to %shell%`
Formal: `terminal.send(Data: text = "ls -la", Process: process = %shell%)`
