Step text: `run terminal 'wsl.exe' with parameters '-d', 'PlangOS', keep running, on output call Frame, write to %container%`
Properties: `{"App": "wsl.exe", "Parameter": ["-d", "PlangOS"], "OnOutput": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "Frame"}]}}` — "keep running" is what makes it `terminal.open`; the trailing `write to %container%` is its own action and holds the running program.

Step text: `run terminal 'wsl.exe' with parameters '-d', 'PlangOS', keep running, binary output, on output call Frame, write to %container%`
Properties: `{"App": "wsl.exe", "Parameter": ["-d", "PlangOS"], "Binary": true, "OnOutput": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "Frame"}]}}` — "binary output": stdout is length-prefixed binary messages, each given to OnOutput as binary `%!data%`.

Step text: `run terminal 'wsl.exe' with parameters '-d', 'PlangOS', keep running, binary output to %screen%, write to %container%`
Properties: `{"App": "wsl.exe", "Parameter": ["-d", "PlangOS"], "Binary": true, "OutputTo": "%screen%"}` — "binary output to %screen%": each message goes straight to the screen (from screen.open), no goal per message.

Step text: `run terminal 'plang', in folder 'worker', keep running, on output call Heard, write to %worker%`
Properties: `{"App": "plang", "WorkingDirectory": "worker", "OnOutput": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "Heard"}]}}` — "in folder …" / "in …" is the folder the program runs in.

Step text: `open 'python' with parameters '-i', on output call Show, on error call ShowError, write to %py%`
Properties: `{"App": "python", "Parameter": ["-i"], "OnOutput": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "Show"}]}, "OnError": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "ShowError"}]}}`
