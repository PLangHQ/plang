Step text: `open screen 'PlangOS', size 1920x1080, on input call Input, on close call Close, write to %screen%`
Formal: `screen.open(Title="PlangOS", Width=1920, Height=1080, OnInput=goal.call(Name="Input"), OnClose=goal.call(Name="Close"))` — the trailing `write to %screen%` is its own action and holds the screen.

Step text: `open screen size 1920x1080, frames to output, write to %screen%`
Formal: `screen.open(Width=1920, Height=1080, ToOutput=true)` — PlangOS's screen (Linux): programs draw onto it; "frames to output": its frames go to this app's own output. What happens to its windows is their own events: `on %browser.window% closed, call X`.
