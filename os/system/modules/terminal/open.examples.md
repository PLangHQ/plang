Step text: `run terminal 'wsl.exe' with parameters '-d', 'PlangOS', keep running, on output call Frame, write to %container%`
Formal: `terminal.open(App="wsl.exe", Parameter=["-d", "PlangOS"], OnOutput=goal.call(Name="Frame"))` — "keep running" is what makes it `terminal.open`; the trailing `write to %container%` is its own action and holds the running program.

Step text: `run terminal 'wsl.exe' with parameters '-d', 'PlangOS', keep running, binary output, on output call Frame, write to %container%`
Formal: `terminal.open(App="wsl.exe", Parameter=["-d", "PlangOS"], Binary=true, OnOutput=goal.call(Name="Frame"))` — "binary output": stdout is length-prefixed binary messages, each given to OnOutput as binary `%!data%`.

Step text: `run terminal 'wsl.exe' with parameters '-d', 'PlangOS', keep running, binary output to %screen%, write to %container%`
Formal: `terminal.open(App="wsl.exe", Parameter=["-d", "PlangOS"], Binary=true, OutputTo=%screen%)` — "binary output to %screen%": each message goes straight to the screen (from screen.open), no goal per message.

Step text: `run terminal 'plang', in folder 'worker', keep running, on output call Heard, write to %worker%`
Formal: `terminal.open(App="plang", WorkingDirectory="worker", OnOutput=goal.call(Name="Heard"))` — "in folder …" / "in …" is the folder the program runs in.

Step text: `open 'python' with parameters '-i', on output call Show, on error call ShowError, write to %py%`
Formal: `terminal.open(App="python", Parameter=["-i"], OnOutput=goal.call(Name="Show"), OnError=goal.call(Name="ShowError"))`
