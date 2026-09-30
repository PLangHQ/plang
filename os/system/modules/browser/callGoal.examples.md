Step text: `call ShowFiles files=%files% in %browser%`
Properties: `{"Name": "ShowFiles", "Parameter": [{"name": "files", "value": "%files%"}], "Browser": "%browser%"}` — browser.callGoal, not goal.call: `in %browser%` puts the goal in the browser's page, not in this app.

Step text: `call Show text=%text%, file=%asked.read% in window %asked.from% of %browser%`
Properties: `{"Name": "Show", "Parameter": [{"name": "text", "value": "%text%"}, {"name": "file", "value": "%asked.read%"}], "Browser": "%browser%", "Window": "%asked.from%"}` — `in window … of %browser%`: the page in that window.

Step text: `call Saved in window %asked.from% of %browser%`
Properties: `{"Name": "Saved", "Browser": "%browser%", "Window": "%asked.from%"}`
