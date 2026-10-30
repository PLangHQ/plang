Step text: `send %!data% to %container%`
Formal: `terminal.send(Data=%!data%, Process=%container%)` — `%container%` holds a running program (from terminal.open).

Step text: `send 'ls -la' to %shell%`
Formal: `terminal.send(Data="ls -la", Process=%shell%)`
