Step text: `on input call Input`
Formal: `on.input(Action=goal.call(Name="Input"))` — no channel or event named: this app's own input, line by line.

Step text: `for each line of input, call HandleLine`
Formal: `on.input(Action=goal.call(Name="HandleLine"))`
