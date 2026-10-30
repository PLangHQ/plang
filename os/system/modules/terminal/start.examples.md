Step text: `run terminal 'git' with parameters 'status', '--short', write to %status%`
Formal: `terminal.start(App="git", Parameter=["status", "--short"])`

Step text: `start 'wsl.exe' --status, env WSL_UTF8=1, write to %wsl%`
Formal: `terminal.start(App="wsl.exe", Parameter=["--status"], Environment={"WSL_UTF8": "1"})`

Step text: `run terminal 'wsl.exe -d PlangOS', interactive`
Formal: `terminal.start(App="wsl.exe", Parameter=["-d", "PlangOS"], Interactive=true)` — the program's name and its arguments are split; each argument is its own list item.

Step text: `run terminal 'wsl.exe --install --no-distribution' as administrator, write to %install%`
Formal: `terminal.start(App="wsl.exe", Parameter=["--install", "--no-distribution"], Administrator=true)`

Step text: `start dotnet build in /src/app, on output call ShowLine, on error call ShowError`
Formal: `terminal.start(App="dotnet", Parameter=["build"], WorkingDirectory="/src/app", OnOutput=goal.call(Name="ShowLine"), OnError=goal.call(Name="ShowError"))` — each runs once per line, the line as `%!data%`.
