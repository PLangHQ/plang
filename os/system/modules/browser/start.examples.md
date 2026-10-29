Step text: `start browser 'https://www.mbl.is', size 1920x1080, on frame call Frame, write to %browser%`
Formal: `browser.start(Url: text = "https://www.mbl.is", Width: number = 1920, Height: number = 1080, OnFrame: action = goal.call(Name: goal = "Frame"))` — the trailing `write to %browser%` is its own action and holds the running browser.

Step text: `start browser 'file:///home/plang/desktop.html' on %screen%, on message call Desktop, write to %browser%`
Formal: `browser.start(Url: text = "file:///home/plang/desktop.html", Screen: screen = %screen%, OnMessage: action = goal.call(Name: goal = "Desktop"))` — "on %screen%": Chromium draws onto PlangOS's screen (from screen.open), which makes the frames.

Step text: `open 'https://plang.is' in a browser, png frames, on frame call Show, write to %page%`
Formal: `browser.start(Url: text = "https://plang.is", Format: text = "png", OnFrame: action = goal.call(Name: goal = "Show"))`
