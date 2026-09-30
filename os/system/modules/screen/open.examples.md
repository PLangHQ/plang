Step text: `open screen 'PlangOS', size 1920x1080, on input call Input, on close call Close, write to %screen%`
Properties: `{"Title": "PlangOS", "Width": 1920, "Height": 1080, "OnInput": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "Input"}]}, "OnClose": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "Close"}]}}` — the trailing `write to %screen%` is its own action and holds the screen.

Step text: `open screen size 1920x1080, frames to output, on window call Window, write to %screen%`
Properties: `{"Width": 1920, "Height": 1080, "ToOutput": true, "OnWindow": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "Window"}]}}` — PlangOS's screen (Linux): programs draw onto it; "frames to output": its frames go to this app's own output.
