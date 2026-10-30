Step text: `load code plugin.dll`
Formal: `code.load(Path="plugin.dll")`

Step text: `code.load Path=plugin.dll, on error key "TypeLoadCollision" call Refused`
Formal: `code.load(Path="plugin.dll")` — the DLL is the Path; the error clause is its own action after it.
