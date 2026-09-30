Step text: `call ShowFiles files=%files% in %browser.desktop%`
Properties: `{"Name": "ShowFiles", "Parameter": [{"name": "files", "value": "%files%"}], "Window": "%browser.desktop%"}` — window.callGoal, not goal.call: `in %window%` puts the goal in the window's page, not in this app.

Step text: `call Show text=%text%, file=%file% in %window%`
Properties: `{"Name": "Show", "Parameter": [{"name": "text", "value": "%text%"}, {"name": "file", "value": "%file%"}], "Window": "%window%"}`

Step text: `call Saved in window %asked.from% of %browser%`
Properties: `{"Name": "Saved", "Window": "%asked.from%", "Browser": "%browser%"}` — a window by its id on the screen: the browser it is in comes with it.
