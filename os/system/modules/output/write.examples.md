Step text: `write out "Welcome back, %user.name%"`

Step text: `show %message% to the user`

Step text: `print %result%`

Step text: `write 'hello' to echo`
Formal: `output.write(Data: text = "hello", channel: text = "echo")` — a bare `to <name>` routes to that channel; the unquoted `echo` is the channel name, never dropped.

Step text: `write %error.Message% to "errors" channel`
