Step text: `list files in docs/ recursive, write to %files%`
Mapping: `file.list Path([path] docs/), Recursive([bool] true) | variable.set Name([string] %files%), Value([object] %!data%)`
