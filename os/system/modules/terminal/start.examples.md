Step text: `run terminal 'git' with parameters 'status', '--short', write to %status%`
Formal: `terminal.start(App: text = "git", Parameter: list = ["status", "--short"])`

Step text: `start 'wsl.exe' --status, env WSL_UTF8=1, write to %wsl%`
Formal: `terminal.start(App: text = "wsl.exe", Parameter: list = ["--status"], Environment: dict = {"WSL_UTF8": "1"})`

Step text: `run terminal 'wsl.exe -d PlangOS', interactive`
Formal: `terminal.start(App: text = "wsl.exe", Parameter: list = ["-d", "PlangOS"], Interactive: bool = true)` — the program's name and its arguments are split; each argument is its own list item.

Step text: `run terminal 'wsl.exe --install --no-distribution' as administrator, write to %install%`
Formal: `terminal.start(App: text = "wsl.exe", Parameter: list = ["--install", "--no-distribution"], Administrator: bool = true)`

Step text: `start dotnet build in /src/app, on output call ShowLine, on error call ShowError`
Formal: `terminal.start(App: text = "dotnet", Parameter: list = ["build"], WorkingDirectory: path = "/src/app", OnOutput: action = goal.call(Name: goal = "ShowLine"), OnError: action = goal.call(Name: goal = "ShowError"))` — each runs once per line, the line as `%!data%`.
