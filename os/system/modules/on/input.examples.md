Step text: `on input call Input`
Properties: `{"Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "Input"}]}}` — no channel or event named: this app's own input, line by line.

Step text: `for each line of input, call HandleLine`
Properties: `{"Action": {"module": "goal", "name": "call", "property": [{"name": "Name", "value": "HandleLine"}]}}`
