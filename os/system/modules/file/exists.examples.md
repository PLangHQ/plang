Step text: `check if file.txt exists, write to %fileInfo%`
Mapping: `file.exists Path([path] file.txt) | variable.set Name([variable] %fileInfo%), Value([path] %!data%)`
