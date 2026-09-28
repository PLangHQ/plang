Step text: `load code plugin.dll`
Properties: `{"Path": "plugin.dll"}`

Step text: `code.load Path=plugin.dll, on error key "TypeLoadCollision" call Refused`
Properties: `{"Path": "plugin.dll"}` — the DLL is the Path; the error clause is its own action after it.
