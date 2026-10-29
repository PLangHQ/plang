Step text: `start browser 'https://www.mbl.is', size 1920x1080, on frame call Frame, write to %browser%`
Formal: `browser.start(Url="https://www.mbl.is", Width=1920, Height=1080, OnFrame=goal.call(Name="Frame"))` — the trailing `write to %browser%` is its own action and holds the running browser.

Step text: `start browser 'file:///home/plang/desktop.html' on %screen%, on message call Desktop, write to %browser%`
Formal: `browser.start(Url="file:///home/plang/desktop.html", Screen=%screen%, OnMessage=goal.call(Name="Desktop"))` — "on %screen%": Chromium draws onto PlangOS's screen (from screen.open), which makes the frames.

Step text: `open 'https://plang.is' in a browser, png frames, on frame call Show, write to %page%`
Formal: `browser.start(Url="https://plang.is", Format="png", OnFrame=goal.call(Name="Show"))`
