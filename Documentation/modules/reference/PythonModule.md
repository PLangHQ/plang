# PythonModule

Hint: `[python]`  
Type: `PLang.Modules.PythonModule.Program`

Runs python scripts. Parameters can be passed to the python process

## Methods

### RunPythonScript

```
RunPythonScript(String fileName = main.py, String[] parameterValues = null, String[] parameterNames = null, String[] variablesToExtractFromPythonScript = null, Boolean useNamedArguments = False, String pythonPath = null, String stdOutVariableName = null, String stdErrorVariableName = null) : object
```

Run a python script. parameterNames should be equal length as parameterValues. Parameter example name=%name%. variablesToExtractFromPythonScript are keys in the format [a-zA-Z0-9_\.]+ that the user want to write to


