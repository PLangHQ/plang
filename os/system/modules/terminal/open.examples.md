Step text: `run terminal 'wsl.exe' with parameters '-d', 'PlangOS', keep running, on output call Frame, write to %container%`
Formal: `terminal.open(App: text = "wsl.exe", Parameter: list = ["-d", "PlangOS"], OnOutput: action = goal.call(Name: goal = "Frame"))` — "keep running" is what makes it `terminal.open`; the trailing `write to %container%` is its own action and holds the running program.

Step text: `run terminal 'wsl.exe' with parameters '-d', 'PlangOS', keep running, binary output, on output call Frame, write to %container%`
Formal: `terminal.open(App: text = "wsl.exe", Parameter: list = ["-d", "PlangOS"], Binary: bool = true, OnOutput: action = goal.call(Name: goal = "Frame"))` — "binary output": stdout is length-prefixed binary messages, each given to OnOutput as binary `%!data%`.

Step text: `run terminal 'wsl.exe' with parameters '-d', 'PlangOS', keep running, binary output to %screen%, write to %container%`
Formal: `terminal.open(App: text = "wsl.exe", Parameter: list = ["-d", "PlangOS"], Binary: bool = true, OutputTo: screen = %screen%)` — "binary output to %screen%": each message goes straight to the screen (from screen.open), no goal per message.

Step text: `run terminal 'plang', in folder 'worker', keep running, on output call Heard, write to %worker%`
Formal: `terminal.open(App: text = "plang", WorkingDirectory: path = "worker", OnOutput: action = goal.call(Name: goal = "Heard"))` — "in folder …" / "in …" is the folder the program runs in.

Step text: `open 'python' with parameters '-i', on output call Show, on error call ShowError, write to %py%`
Formal: `terminal.open(App: text = "python", Parameter: list = ["-i"], OnOutput: action = goal.call(Name: goal = "Show"), OnError: action = goal.call(Name: goal = "ShowError"))`
