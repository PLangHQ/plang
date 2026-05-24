Step text: `ask user 'what's your name?', write to %name%`
Mapping: `output.ask Question([string] what's your name?) | variable.set Name([string] %name%), Value([object] %!data%)`

Step text: `output.ask question='Allow access? (y/n/a)', write to %answer%`
Mapping: `output.ask Question([string] Allow access? (y/n/a)) | variable.set Name([string] %answer%), Value([object] %!data%)`
